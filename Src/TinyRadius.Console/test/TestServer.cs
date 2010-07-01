/**
 * $Id: TestServer.java,v 1.6 2006/02/17 18:14:54 wuttke Exp $
 * Created on 08.04.2005
 * @author Matthias Wuttke
 * @version $Revision: 1.6 $
 */
namespace TinyRadius.Test
{

    using System.IO;
    using java.net.InetSocketAddress;

    using TinyRadius.packet.AccessRequest;
    using TinyRadius.packet.RadiusPacket;
    using TinyRadius.util.RadiusException;
    using TinyRadius.util.RadiusServer;
    using System;
    using TinyRadius.util;
    using TinyRadius.Packet;
    using TinyRadius.packet;
    using TinyRadius.Util;

    /**
     * Test server which terminates after 30 s.
     * Knows only the client "localhost" with secret "testing123" and
     * the user "mw" with the password "test".
     */
    public class TestServer
    {

        public static void main(String[] args)
        {

            RadiusServer server = new RadiusServer();
            /* {
                 // Authorize localhost/testing123
                 public String getSharedSecret(InetSocketAddress client) {
                     if (client.getAddress().getHostAddress().equals("127.0.0.1"))
                         return "testing123";
                     else
                         return null;
                 }
			
                 // Authenticate mw
                 public String getUserPassword(String userName) {
                     if (userName.equals("mw"))
                         return "test";
                     else
                         return null;
                 }
			
                 // Adds an attribute to the Access-Accept packet
                 public RadiusPacket accessRequestReceived(AccessRequest accessRequest, InetSocketAddress client) 
                 {
                
                     Console.WriteLine("Received Access-Request:\n" + accessRequest);
                     RadiusPacket packet = super.accessRequestReceived(accessRequest, client);
                     if (packet.getPacketType() == RadiusPacket.ACCESS_ACCEPT)
                         packet.addAttribute("Reply-Message", "Welcome " + accessRequest.getUserName() + "!");
                     if (packet == null)
                         Console.WriteLine("Ignore packet.");
                     else
                         Console.WriteLine("Answer:\n" + packet);
                     return packet;
                 }
             };*/
            if (args.length >= 1)
                server.setAuthPort(Integer.parseInt(args[0]));
            if (args.length >= 2)
                server.setAcctPort(Integer.parseInt(args[1]));

            server.start(true, true);

            Console.WriteLine("Server started.");

            Thread.sleep(1000 * 60 * 30);
            Console.WriteLine("Stop server");
            server.stop();
        }

    }
}