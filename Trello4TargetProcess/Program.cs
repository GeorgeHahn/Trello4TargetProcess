using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using TrelloNet;
using Newtonsoft.Json;
using RestSharp.Deserializers;

namespace Trello4TargetProcess
{
    class Program
    {
        static void Main(string[] args)
        {
            System.Action showHelp = () =>
            {
                Console.WriteLine();
                Console.WriteLine("Usage:");
                Console.WriteLine("\tSet appkey:\n\t\t Trello4TargetProcess api [appkey]\n\t\t(from https://trello.com/1/appKey/generate)");
                Console.WriteLine("\tRequest authorization:\n\t\t Trello4TargetProcess getauth");
                Console.WriteLine("\tAuthorize:\n\t\t Trello4TargetProcess auth [authkey]");
                Console.WriteLine("\tSet TargetProcess URL:\n\t\t Trello4TargetProcess tp [targetprocess url]");
                Console.WriteLine("\tSet TargetProcess auth token:\n\t\t Trello4TargetProcess tptoken [targetprocess auth token]");
                Console.WriteLine("\tRun:\n\t\t Trello4TargetProcess board [boardid]");
            };

            var settings = JsonConvert.DeserializeObject<Settings>(File.ReadAllText("settings.json"));

            if (settings.PollInterval == 0)
                settings.PollInterval = 300000;

            if (args.Length > 0)
            {
                var param = args[0];

                if (param.Contains("help") || args.Length == 1)
                {
                    showHelp();
                    return;
                }

                var val = args[1];

                switch (param)
                {
                    case "api":
                        settings.TrelloAppKey = val;
                        break;
                    case "board":
                        settings.TrelloBoardID = val;
                        break;
                    case "tp":
                        settings.TargetProcessURL = val;
                        break;
                    case "gettoken":
                        if(string.IsNullOrWhiteSpace(settings.TargetProcessURL))
                        {
                            Console.WriteLine("Must set TargetProcess URL first");
                            showHelp();
                            return;
                        }
                        Console.WriteLine(File.ReadAllText("tpurl.cfg") + "/api/v1/Authentication?format=json");
                        return;
                    case "tptoken":
                        settings.TargetProcessToken = val;
                        break;
                    case "auth":
                    case "getauth":
                        if (string.IsNullOrWhiteSpace(settings.TrelloAppKey))
                        {
                            Console.WriteLine("API key not set.");
                            showHelp();
                            return;
                        }
                        var config = new Trello(settings.TrelloAppKey);

                        if (param == "getauth")
                        {
                            var authurl = config.GetAuthorizationUrl("Trello4TargetProcess", Scope.ReadWrite,
                                Expiration.Never);
                            Console.WriteLine("Authorization URL recieved");
                            Console.WriteLine(authurl);
                        }

                        if (param == "auth")
                        {
                            settings.TrelloAuthKey = args[1];
                            config.Authorize(args[1]);
                            Console.WriteLine("Successfully authorized");
                        }
                        break;
                }

                File.WriteAllText("settings.json", JsonConvert.SerializeObject(settings));
                Console.WriteLine("Settings written.");
                return;
            }

            // API key
            if (string.IsNullOrWhiteSpace(settings.TrelloAppKey))
            {
                Console.WriteLine("Trello app key key not set.");
                return;
            }

            // Boad id
            if (string.IsNullOrWhiteSpace(settings.TrelloBoardID))
            {
                Console.WriteLine("Board id not set.");
                return;
            }

            // Auth
            if (string.IsNullOrWhiteSpace(settings.TrelloAuthKey))
            {
                Console.WriteLine("Not authorized.");
                return;
            }

            if (string.IsNullOrWhiteSpace(settings.TargetProcessURL))
            {
                Console.WriteLine("No targetprocess URL.");
                return;
            }

            if (string.IsNullOrWhiteSpace(settings.TargetProcessToken))
            {
                Console.WriteLine("No targetprocess token.");
                return;
            }

            var trello = new Trello(settings.TrelloAppKey);
            trello.Authorize(settings.TrelloAuthKey);

            var tpclient = new TargetProcess(settings.TargetProcessURL, settings.TargetProcessToken);

            var glue = new TrelloTargetProcessGlueEngine(trello, settings.TrelloBoardID, tpclient, settings);

            while (true)
            {
                Console.WriteLine("Running");
                glue.Run();
                Console.WriteLine("Sleeping");
                Thread.Sleep(settings.PollInterval);
            }
        }

        public class Settings
        {
            public string TrelloAppKey { get; set; }
            public string TrelloAuthKey { get; set; }
            public string TrelloBoardID { get; set; }

            public string TargetProcessURL { get; set; }
            public string TargetProcessToken { get; set; }
            public TargetProcess.Project TargetProcessProject { get; set; }

            public int PollInterval { get; set; }
        }
    }
}
