using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YoloBeer
{
    class Program
    {
        public static int Main(string[] args)
        {
            string tmpCfg, tmpWeights, tmpNames;
            if (args.Length == 3)
            {
                tmpCfg = args[0];
                tmpWeights = args[1];
                tmpNames = args[2];
            }
            else
            {
                //Starting Darknet
                Console.WriteLine("*** YOLO CONFIGURATION ***");
                Console.WriteLine("Yolo CFG File: ");
                tmpCfg = "yolo-obj.cfg";
                Console.WriteLine("Yolo WEIGHTS File: ");
                tmpWeights = "yolo-obj_21000.weights";
                Console.WriteLine("Yolo NAMES File: ");
                tmpNames = "obj.names";
            }

            Console.WriteLine("Initializing YOLO  ...");
            Yolo.InitDnn(tmpCfg, tmpWeights, tmpNames, 0);

            //Starting MQTTServer
            MqttClient.StartClient();

            Task.Run(async() =>
            {
                while (true)
                {
                    MqttClient.ProcessEvents();
                }
            });

            //Handling console commands
            var command = Console.ReadLine()?.ToLower();

            while (command != "stop")
            {
                switch (command)
                {
                    case "help":
                        PrintHelp();
                        break;
                    default:
                        Console.WriteLine("Invalid command. Type 'help' for a list of available commands");
                        break;
                }
                command = Console.ReadLine()?.ToLower();
            }

            //Stopping the server
            Console.WriteLine("Stopping client ...");
            MqttClient.StopClient();
            return 0;
        }

        private static void PrintHelp()
        {
            Console.WriteLine("Available commands: ");
            Console.WriteLine("help\tShows this page");
            Console.WriteLine("stop\tStops the server");

        }
    }
}
