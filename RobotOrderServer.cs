// RobotOrderServer.cs is sending the orders to the robot.
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace Login;

public static class RobotOrderServer
{
    // Receiving a list of components.
    public static void LoadOrder(List<string> components)
    {
        // Connect to the robot.
        using var client = new TcpClient("172.20.254.203", 30002);
        using var stream = client.GetStream();

        // Sending each action to the robot one at a time.
        foreach (var component in components)
        {
            // Converts the data.
            var msg = Encoding.ASCII.GetBytes(component + "\n");
            stream.Write(msg, 0, msg.Length);
        }
    }

}
