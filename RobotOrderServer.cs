using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace Login;   // ⚠️ vigtigt: samme namespace som MainWindow

public static class RobotOrderServer
{
    public static void LoadOrder(List<string> components)
    {
        using var client = new TcpClient("172.20.254.203", 30002);
        using var stream = client.GetStream();

        foreach (var component in components)
        {
            var msg = Encoding.ASCII.GetBytes(component + "\n");
            stream.Write(msg, 0, msg.Length);
        }
    }
}