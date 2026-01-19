using System.Net.Sockets;
using System.Text;

namespace Login;

//<Summary>
// Program til at conecte robotten med vores GUI 
// Understøtter kørsel af enkeltbevægelser samt ordreafvikling af komponenter i ønskede mængder
// Indeholder pick&place-sekevenser og styring af robotten samt gripper. 


//Her konstateres IP-adressen på robotten samt portene 
    //Port 30002: Sender URScript som tekst. 
public static class RobotConnectionTest
{
    private const string RobotIp = "172.20.254.203";
    private const int UrScriptPort = 30002;
    private const int DashboardPort = 29999;

    //Her oprettes en TCP-forbindelse til RobotIp 
    // using lukker forbindelse automatisk bagefter 
    //Flush() sikrer at data bliver sendt med det samme.     
    private static void SendString(int port, string message)
    {
        using var client = new TcpClient(RobotIp, port);
        using var stream = client.GetStream();

        var bytes = Encoding.ASCII.GetBytes(message);
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush();
    }

    //URScript-programmet sendes som tekst til robotten via TCP. 
    // Robotten læser typisk input linje for linje, så derfor slutter programmet med et linjeskift \n
    private static void SendProgram(string urScriptProgram)
    {
        if (!urScriptProgram.EndsWith("\n")) urScriptProgram += "\n";
        SendString(UrScriptPort, urScriptProgram);
    }

    // Dashboard
    //Der sendes helt almindelige tekst-kommander (strings) til robotten på Dashboard server-porten 
        //Dashboard-serveren forstår bestemte ord som "stop"=stop robotten 
        //Hver kommando skal afsluttes med \n, fordi robotten læser komandoer linje for linje. 
    public static void BrakeRelease() => SendString(DashboardPort, "brake release\n");
    public static void StopRobot() => SendString(DashboardPort, "stop\n");

    // Her sendes et programmet afsted. 
    public static void MoveToP2_Single()
    {
        SendProgram(
            "def prog():\n" +
            "  movej(p[0.482,-0.118,0.044,3.182,-0.003,-0.009], a=1.2, v=0.25)\n" +
            "end\n" +
            "prog()\n"
        );
    }

    // Enkelt-komponent kald (genbrug RunOrder)
    public static void RunComponentA() => RunOrder(1, 0, 0);
    public static void RunComponentB() => RunOrder(0, 1, 0);
    public static void RunComponentC() => RunOrder(0, 0, 1);
    public static void RunAll()        => RunOrder(1, 1, 1);

    // Kør flere i samme ordre
    public static void RunOrder(int qtyA, int qtyB, int qtyC)
    {
        SendProgram(BuildOrderProgram(qtyA, qtyB, qtyC));
    }

    // Input - Her undergår vi negative mængder
    private static string BuildOrderProgram(int qtyA, int qtyB, int qtyC)
    {
        //Hvilke komponent-funktioner der skal kaldes 
        // Bygger en tekstsekevens inde i prog() 
        var run = "";
        for (int i = 0; i < qtyA; i++) run += "  do_A()\n";
        for (int i = 0; i < qtyB; i++) run += "  do_B()\n";
        for (int i = 0; i < qtyC; i++) run += "  do_C()\n";

        // Bevægelser 
            // p1 .. ... .. , p9 er fastsatte punkter 
            // a = acceleration , v = hastighed 

        //Robot og gripper 
            // Opretter RPC-klient mod service på robotten 
            // TOOL_INDEX=0 betyder "Første gripper/enhed"
            // rg_is_busy() spørger om gripper er optaget 
            // rg_grip(width, force) sender grip-kommando og venter indtil gripper er færdig 
            // "+ 0.0" tvinger tal til float (URScript kan være følsom)
        return
            "def prog():\n" +
            "  p1 = p[0.130,-0.345,0.548,2.01,-0.001,-0.007]\n" +
            "  p2 = p[0.482,-0.118,0.044,3.182,-0.003,-0.009]\n" +
            "  p6 = p[0.482,-0.118,-0.125,3.182,-0.003,-0.009]\n" +
            "  p3 = p[0.425,-0.225,0.044,3.146,-0.478,-0.001]\n" +
            "  p7 = p[0.425,-0.225,-0.125,3.146,-0.478,-0.001]\n" +
            "  p4 = p[0.292,-0.385,0.044,2.972,-1.166,-0.041]\n" +
            "  p8 = p[0.292,-0.385,-0.125,2.972,-1.166,-0.041]\n" +
            "  p5 = p[0.027,-0.482,0.044,2.508,-1.984,-0.015]\n" +
            "  p9 = p[0.027,-0.482,-0.05,2.508,-1.984,-0.015]\n" +
            "  a = 1.2\n" +
            "  v = 0.25\n" +
            "\n" +
            "  global RPC = rpc_factory(\"xmlrpc\", \"http://172.20.254.203:41414\")\n" +
            "  global TOOL_INDEX = 0\n" +
            "  def rg_is_busy():\n" +
            "    return RPC.rg_get_busy(TOOL_INDEX)\n" +
            "  end\n" +
            "  def rg_grip(width, force=20):\n" +
            "    RPC.rg_grip(TOOL_INDEX, width + 0.0, force + 0.0)\n" +
            "    sleep(0.01)\n" +
            "    while (rg_is_busy()):\n" +
            "      sleep(0.01)\n" +
            "    end\n" +
            "  end\n" +
            "\n" +
            //--- pick & place sekvenser ---
            "  def do_A():\n" +
            "    movej(p1, a=a, v=v)\n" +
            "    movej(p2, a=a, v=v)\n" +
            "    movel(p6, a=a, v=v)\n" +
            "    rg_grip(50)\n" +
            "    rg_grip(32)\n" +
            "    movel(p2, a=a, v=v)\n" +
            "    movej(p5, a=a, v=v)\n" +
            "    movel(p9, a=a, v=v)\n" +
            "    rg_grip(50)\n" +
            "    movel(p5, a=a, v=v)\n" +
            "    movej(p1, a=a, v=v)\n" +
            "  end\n" +
            "\n" +
            "  def do_B():\n" +
            "    movej(p1, a=a, v=v)\n" +
            "    movej(p3, a=a, v=v)\n" +
            "    movel(p7, a=a, v=v)\n" +
            "    rg_grip(50)\n" +
            "    rg_grip(11)\n" +
            "    movel(p3, a=a, v=v)\n" +
            "    movej(p5, a=a, v=v)\n" +
            "    movel(p9, a=a, v=v)\n" +
            "    rg_grip(50)\n" +
            "    movel(p5, a=a, v=v)\n" +
            "    movej(p1, a=a, v=v)\n" +
            "  end\n" +
            "\n" +
            "  def do_C():\n" +
            "    movej(p1, a=a, v=v)\n" +
            "    movej(p4, a=a, v=v)\n" +
            "    movel(p8, a=a, v=v)\n" +
            "    rg_grip(50)\n" +
            "    rg_grip(32)\n" +
            "    movel(p4, a=a, v=v)\n" +
            "    movej(p5, a=a, v=v)\n" +
            "    movel(p9, a=a, v=v)\n" +
            "    rg_grip(50)\n" +
            "    movel(p5, a=a, v=v)\n" +
            "    movej(p1, a=a, v=v)\n" +
            "  end\n" +
            "\n" +
            //--- Kør kommando ---
            run +
            "end\n" +
            "prog()\n";
    }
}

