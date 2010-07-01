using TinyRadius.Net.Attributes;
using TinyRadius.Net.Packet;
using System;
using TinyRadius.Net.packet;
using System.Threading;
using TinyRadius.Net.Net.JavaHelper;
using TinyRadius.Net.JavaHelper;
using log4net;
using System.Collections;


namespace TinyRadius.Net.Util
{

    /*using java.io.ByteArrayInputStream;
    using java.io.ByteArrayOutputStream;
    using java.io.IOException;
    using java.net.DatagramPacket;
    using java.net.DatagramSocket;
    using java.net.InetAddress;
    using java.net.InetSocketAddress;
    using java.net.SocketException;
    using java.net.SocketTimeoutException;
    using java.Util.Arrays;
    using java.Util.Iterator;
    using java.Util.LinkedList;
    using java.Util.ArrayList;

    using org.apache.commons.logging.Log;
    using org.apache.commons.logging.LogFactory;*/


    /**
     * Implements a simple Radius server. This class must be subclassed to
     * provide an implementation for getSharedSecret() and getUserPassword().
     * If the server supports accounting, it must override
     * accountingRequestReceived().
     */
    public abstract class RadiusServer
    {

        /**
         * Returns the shared secret used to communicate with the client with the
         * passed IP address or null if the client is not allowed at this server.
         * @param client IP address and port number of client
         * @return shared secret or null
         */
        public abstract String getSharedSecret(InetSocketAddress client);

        /**
         * Returns the password of the passed user. Either this
         * method or accessRequestReceived() should be overriden.
         * @param userName user name
         * @return plain-text password or null if user unknown
         */
        public abstract String getUserPassword(String userName);

        /**
         * Constructs an answer for an Access-Request packet. Either this
         * method or isUserAuthenticated should be overriden.
         * @param accessRequest Radius request packet
         * @param client address of Radius client
         * @return response packet or null if no packet shall be sent
         * @exception RadiusException malformed request packet; if this
         * exception is thrown, no answer will be sent
         */
        public RadiusPacket accessRequestReceived(AccessRequest accessRequest, System.Net.IPAddress client)
        {
            String plaintext = getUserPassword(accessRequest.getUserName());
            int type = RadiusPacket.ACCESS_REJECT;
            if (plaintext != null && accessRequest.verifyPassword(plaintext))
                type = RadiusPacket.ACCESS_ACCEPT;

            RadiusPacket answer = new RadiusPacket(type, accessRequest.Identifier);
            copyProxyState(accessRequest, answer);
            return answer;
        }

        /**
         * Constructs an answer for an Accounting-Request packet. This method
         * should be overriden if accounting is supported.
         * @param accountingRequest Radius request packet
         * @param client address of Radius client
         * @return response packet or null if no packet shall be sent
         * @exception RadiusException malformed request packet; if this
         * exception is thrown, no answer will be sent
         */
        public RadiusPacket accountingRequestReceived(AccountingRequest accountingRequest, InetSocketAddress client)
        {
            RadiusPacket answer = new RadiusPacket(RadiusPacket.ACCOUNTING_RESPONSE, accountingRequest.Identifier);
            copyProxyState(accountingRequest, answer);
            return answer;
        }

        /**
         * Starts the Radius server.
         * @param listenAuth open auth port?
         * @param listenAcct open acct port?
         */
        public void start(bool listenAuth, bool listenAcct)
        {
            if (listenAuth)
            {
                ThreadPool.QueueUserWorkItem(delegate(object state)
                {
                    setName("Radius Auth Listener");
                    try
                    {
                        logger.info("starting RadiusAuthListener on port " + getAuthPort());
                        listenAuth();
                        logger.info("RadiusAuthListener is being terminated");
                    }
                    catch (Exception e)
                    {
                        e.printStackTrace();
                        logger.fatal("auth thread stopped by exception", e);
                    }
                    finally
                    {
                        authSocket.close();
                        logger.debug("auth socket closed");
                    }
                });
                /*new Thread() {
                    public void run() {
                        setName("Radius Auth Listener");
                        try {
                            logger.info("starting RadiusAuthListener on port " + getAuthPort());
                            listenAuth();
                            logger.info("RadiusAuthListener is being terminated");
                        } catch(Exception e) {
                            e.printStackTrace();
                            logger.fatal("auth thread stopped by exception", e);
                        } finally {
                            authSocket.close();
                            logger.debug("auth socket closed");
                        }
                    }
                }.start();*/
            }

            if (listenAcct)
            {
                ThreadPool.QueueUserWorkItem(delegate(object state)
                {
                    setName("Radius Acct Listener");
                    try
                    {
                        logger.info("starting RadiusAcctListener on port " + getAcctPort());
                        listenAcct();
                        logger.info("RadiusAcctListener is being terminated");
                    }
                    catch (Exception e)
                    {
                        e.printStackTrace();
                        logger.fatal("acct thread stopped by exception", e);
                    }
                    finally
                    {
                        acctSocket.close();
                        logger.debug("acct socket closed");
                    }
                });
                /*new Thread() {
                    public void run() {
                        setName("Radius Acct Listener");
                        try {
                            logger.info("starting RadiusAcctListener on port " + getAcctPort());
                            listenAcct();
                            logger.info("RadiusAcctListener is being terminated");
                        } catch(Exception e) {
                            e.printStackTrace();
                            logger.fatal("acct thread stopped by exception", e);
                        } finally {
                            acctSocket.close();
                            logger.debug("acct socket closed");
                        }
                    }
                }.start();*/
            }
        }

        /**
         * Stops the server and closes the sockets.
         */
        public void stop()
        {
            logger.info("stopping Radius server");
            closing = true;
            if (authSocket != null)
                authSocket.close();
            if (acctSocket != null)
                acctSocket.close();
        }

        /**
         * Returns the auth port the server will listen on.
         * @return auth port
         */
        public int getAuthPort()
        {
            return authPort;
        }

        /**
         * Sets the auth port the server will listen on.
         * @param authPort auth port, 1-65535
         */
        public void setAuthPort(int authPort)
        {
            if (authPort < 1 || authPort > 65535)
                throw new ArgumentException("bad port number");
            this.authPort = authPort;
            this.authSocket = null;
        }

        /**
         * Returns the socket timeout (ms).
         * @return socket timeout
         */
        public int getSocketTimeout()
        {
            return socketTimeout;
        }

        /**
         * Sets the socket timeout.
         * @param socketTimeout socket timeout, >0 ms
         * @throws SocketException
         */
        public void setSocketTimeout(int socketTimeout)
        {
            if (socketTimeout < 1)
                throw new ArgumentException("socket tiemout must be positive");
            this.socketTimeout = socketTimeout;
            if (authSocket != null)
                authSocket.setSoTimeout(socketTimeout);
            if (acctSocket != null)
                acctSocket.setSoTimeout(socketTimeout);
        }

        /**
         * Sets the acct port the server will listen on.
         * @param acctPort acct port 1-65535
         */
        public void setAcctPort(int acctPort)
        {
            if (acctPort < 1 || acctPort > 65535)
                throw new ArgumentException("bad port number");
            this.acctPort = acctPort;
            this.acctSocket = null;
        }

        /**
         * Returns the acct port the server will listen on.
         * @return acct port
         */
        public int getAcctPort()
        {
            return acctPort;
        }

        /**
         * Returns the duplicate interval in ms.
         * A packet is discarded as a duplicate if in the duplicate interval
         * there was another packet with the same identifier originating from the
         * same address.
         * @return duplicate interval (ms)
         */
        public long getDuplicateInterval()
        {
            return duplicateInterval;
        }

        /**
         * Sets the duplicate interval in ms.
         * A packet is discarded as a duplicate if in the duplicate interval
         * there was another packet with the same identifier originating from the
         * same address.
         * @param duplicateInterval duplicate interval (ms), >0
         */
        public void setDuplicateInterval(long duplicateInterval)
        {
            if (duplicateInterval <= 0)
                throw new ArgumentException("duplicate interval must be positive");
            this.duplicateInterval = duplicateInterval;
        }

        /**
         * Returns the IP address the server listens on.
         * Returns null if listening on the wildcard address.
         * @return listen address or null
         */
        public InetAddress getListenAddress()
        {
            return listenAddress;
        }

        /**
         * Sets the address the server listens on.
         * Must be called before start().
         * Defaults to null, meaning listen on every
         * local address (wildcard address).
         * @param listenAddress listen address or null
         */
        public void setListenAddress(InetAddress listenAddress)
        {
            this.listenAddress = listenAddress;
        }

        /**
         * Copies all Proxy-State attributes from the request
         * packet to the response packet.
         * @param request request packet
         * @param answer response packet
         */
        protected void copyProxyState(RadiusPacket request, RadiusPacket answer)
        {
            ArrayList proxyStateAttrs = request.GetAttributes(33);
            for (Iterator i = proxyStateAttrs.iterator(); i.hasNext(); )
            {
                RadiusAttribute proxyStateAttr = (RadiusAttribute)i.next();
                answer.AddAttribute(proxyStateAttr);
            }
        }

        /**
         * Listens on the auth port (blocks the current thread).
         * Returns when stop() is called.
         * @throws SocketException
         * @throws InterruptedException
         * 
         */
        protected void listenAuth()
        {
            listen(getAuthSocket());
        }

        /**
         * Listens on the acct port (blocks the current thread).
         * Returns when stop() is called.
         * @throws SocketException
         * @throws InterruptedException
         */
        protected void listenAcct()
        {
            listen(getAcctSocket());
        }

        /**
         * Listens on the passed socket, blocks until stop() is called.
         * @param s socket to listen on
         */
        protected void listen(DatagramSocket s)
        {
            DatagramPacket packetIn = new DatagramPacket
                (new byte[RadiusPacket.MaxPacketLength], RadiusPacket.MaxPacketLength);
            while (true)
            {
                try
                {
                    // receive packet
                    try
                    {
                        logger.trace("about to call socket.receive()");
                        s.receive(packetIn);
                        if (logger.isDebugEnabled())
                            logger.debug("receive buffer size = " + s.getReceiveBufferSize());
                    }
                    catch (SocketException se)
                    {
                        if (closing)
                        {
                            // end thread
                            logger.info("got closing signal - end listen thread");
                            return;
                        }
                        else
                        {
                            // retry s.receive()
                            logger.error("SocketException during s.receive() -> retry", se);
                            continue;
                        }
                    }

                    // check client
                    InetSocketAddress localAddress = (InetSocketAddress)s.getLocalSocketAddress();
                    InetSocketAddress remoteAddress = new InetSocketAddress(packetIn.getAddress(), packetIn.getPort());
                    String secret = getSharedSecret(remoteAddress);
                    if (secret == null)
                    {
                        if (logger.isInfoEnabled())
                            logger.info("ignoring packet from unknown client " + remoteAddress + " received on local address " + localAddress);
                        continue;
                    }

                    // parse packet
                    RadiusPacket request = makeRadiusPacket(packetIn, secret);
                    if (logger.isInfoEnabled())
                        logger.info("received packet from " + remoteAddress + " on local address " + localAddress + ": " + request);

                    // handle packet
                    logger.trace("about to call RadiusServer.handlePacket()");
                    RadiusPacket response = handlePacket(localAddress, remoteAddress, request, secret);

                    // send response
                    if (response != null)
                    {
                        if (logger.isInfoEnabled())
                            logger.info("send response: " + response);
                        DatagramPacket packetOut = makeDatagramPacket(response, secret, remoteAddress.getAddress(), packetIn.getPort(), request);
                        s.send(packetOut);
                    }
                    else
                        logger.info("no response sent");
                }
                catch (SocketTimeoutException ste)
                {
                    // this is expected behaviour
                    logger.trace("normal socket timeout");
                }
                catch (IOException ioe)
                {
                    // error while reading/writing socket
                    logger.error("communication error", ioe);
                }
                catch (RadiusException re)
                {
                    // malformed packet
                    logger.error("malformed Radius packet", re);
                }
            }
        }

        /**
         * Handles the received Radius packet and constructs a response. 
         * @param localAddress local address the packet was received on
         * @param remoteAddress remote address the packet was sent by
         * @param request the packet
         * @return response packet or null for no response
         * @throws RadiusException
         */
        protected RadiusPacket handlePacket(InetSocketAddress localAddress,
            InetSocketAddress remoteAddress, RadiusPacket request, String sharedSecret)
        {
            RadiusPacket response = null;

            // check for duplicates
            if (!isPacketDuplicate(request, remoteAddress))
            {
                if (localAddress.getPort() == getAuthPort())
                {
                    // handle packets on auth port
                    if (GetType(AccessRequest).IsInstanceOfType(request))
                        response = accessRequestReceived((AccessRequest)request, remoteAddress);
                    else
                        logger.error("unknown Radius packet type: " + request.Type);
                }
                else if (localAddress.getPort() == getAcctPort())
                {
                    // handle packets on acct port
                    if (typeof(AccountingRequest).IsInstanceOfType(request))
                        response = accountingRequestReceived((AccountingRequest)request, remoteAddress);
                    else
                        logger.error("unknown Radius packet type: " + request.Type);
                }
                else
                {
                    // ignore packet on unknown port
                }
            }
            else
                logger.info("ignore duplicate packet");

            return response;
        }

        /**
         * Returns a socket bound to the auth port.
         * @return socket
         * @throws SocketException
         */
        protected DatagramSocket getAuthSocket()
        {
            if (authSocket == null)
            {
                if (getListenAddress() == null)
                    authSocket = new DatagramSocket(getAuthPort());
                else
                    authSocket = new DatagramSocket(getAuthPort(), getListenAddress());
                authSocket.setSoTimeout(getSocketTimeout());
            }
            return authSocket;
        }

        /**
         * Returns a socket bound to the acct port.
         * @return socket
         * @throws SocketException
         */
        protected DatagramSocket getAcctSocket()
        {
            if (acctSocket == null)
            {
                if (getListenAddress() == null)
                    acctSocket = new DatagramSocket(getAcctPort());
                else
                    acctSocket = new DatagramSocket(getAcctPort(), getListenAddress());
                acctSocket.setSoTimeout(getSocketTimeout());
            }
            return acctSocket;
        }

        /**
         * Creates a Radius response datagram packet from a RadiusPacket to be send. 
         * @param packet RadiusPacket
         * @param secret shared secret to encode packet
         * @param address where to send the packet
         * @param port destination port
         * @param request request packet
         * @return new datagram packet
         * @throws IOException
         */
        protected DatagramPacket makeDatagramPacket(RadiusPacket packet, String secret, InetAddress address, int port, RadiusPacket request)
        {
            ByteArrayOutputStream bos = new ByteArrayOutputStream();
            packet.EncodeResponsePacket(bos, secret, request);
            byte[] data = bos.toByteArray();
            DatagramPacket datagram = new DatagramPacket(data, data.Length, address, port);
            return datagram;

        }

        /**
         * Creates a RadiusPacket for a Radius request from a received
         * datagram packet.
         * @param packet received datagram
         * @return RadiusPacket object
         * @exception RadiusException malformed packet
         * @exception IOException communication error (after getRetryCount()
         * retries)
         */
        protected RadiusPacket makeRadiusPacket(DatagramPacket packet, String sharedSecret)
        {
            ByteArrayInputStream @in = new ByteArrayInputStream(packet.getData());
            return RadiusPacket.DecodeRequestPacket(@in, sharedSecret);
        }

        /**
         * Checks whether the passed packet is a duplicate.
         * A packet is duplicate if another packet with the same identifier
         * has been sent from the same host in the last time. 
         * @param packet packet in question
         * @param address client address
         * @return true if it is duplicate
         */
        protected bool isPacketDuplicate(RadiusPacket packet, InetSocketAddress address)
        {
            long now = System.currentTimeMillis();
            long intervalStart = now - getDuplicateInterval();

            byte[] authenticator = packet.getAuthenticator();

            lock (receivedPackets)
            {
                for (Iterator i = receivedPackets.iterator(); i.hasNext(); )
                {
                    ReceivedPacket p = (ReceivedPacket)i.next();
                    if (p.receiveTime < intervalStart)
                    {
                        // packet is older than duplicate interval
                        i.remove();
                    }
                    else
                    {
                        if (p.address.equals(address) && p.packetIdentifier == packet.Identifier)
                        {
                            if (authenticator != null && p.authenticator != null)
                            {
                                // packet is duplicate if stored authenticator is equal
                                // to the packet authenticator
                                return Arrays.equals(p.authenticator, authenticator);
                            }
                            else
                            {
                                // should not happen, packet is duplicate
                                return true;
                            }
                        }
                    }
                }

                // add packet to receive list
                ReceivedPacket rp = new ReceivedPacket();
                rp.address = address;
                rp.packetIdentifier = packet.Identifier;
                rp.receiveTime = now;
                rp.authenticator = authenticator;
                receivedPackets.add(rp);
            }

            return false;
        }

        private InetAddress listenAddress = null;
        private int authPort = 1812;
        private int acctPort = 1813;
        private DatagramSocket authSocket = null;
        private DatagramSocket acctSocket = null;
        private int socketTimeout = 3000;
        private ArrayList receivedPackets = new ArrayList();
        private long duplicateInterval = 30000; // 30 s
        private bool closing = false;
        private static ILog logger = LogManager.GetLog(typeof(RadiusServer));

    }

    /**
     * This internal class represents a packet that has been received by 
     * the server.
     */
    class ReceivedPacket
    {

        /**
         * The identifier of the packet.
         */
        public int packetIdentifier;

        /**
         * The time the packet was received.
         */
        public long receiveTime;

        /**
         * The address of the host who sent the packet.
         */
        public InetSocketAddress address;

        /**
         * Authenticator of the received packet.
         */
        public byte[] authenticator;

    }
}