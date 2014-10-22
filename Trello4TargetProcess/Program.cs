using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TrelloNet;

namespace Trello4TargetProcess
{
    class Program
    {
        static void Main(string[] args)
        {
            System.Action showHelp = () =>
            {
                Console.WriteLine("Must be caleed with an application key");
                Console.WriteLine("Get yours at https://trello.com/1/appKey/generate");
                Console.WriteLine();
                Console.WriteLine("Usage:");
                Console.WriteLine("\tSet appkey:\n\t\t Trello4TargetProcess api [appkey]");
                Console.WriteLine("\tRequest authorization:\n\t\t Trello4TargetProcess getauth");
                Console.WriteLine("\tAuthorize:\n\t\t Trello4TargetProcess auth [authkey]");
                Console.WriteLine("\tRun:\n\t\t Trello4TargetProcess board [boardid]");
            };

            if (args.Length > 0)
            {
                if (args[0].Contains("help"))
                {
                    showHelp();
                    return;
                }

                if (args[0] == "api")
                {
                    File.WriteAllText("api.cfg", args[1]);
                    return;
                }

                if (args[0] == "board")
                {
                    File.WriteAllText("boardid.cfg", args[1]);
                    return;
                }

                // Everything below requires a valid API key

                if (!File.Exists("api.cfg"))
                {
                    Console.WriteLine("API key not set.");
                    showHelp();
                    return;
                }
                var config = new Trello(File.ReadAllText("api.cfg"));

                if (args[0] == "getauth")
                {
                    var authurl = config.GetAuthorizationUrl("Trello4TargetProcess", Scope.ReadWrite, Expiration.Never);
                    Console.WriteLine("Authorization URL recieved");
                    Console.WriteLine(authurl);
                    return;
                }

                if (args[0] == "auth")
                {
                    File.WriteAllText("auth.cfg", args[1]);
                    config.Authorize(args[1]);
                    Console.WriteLine("Successfully authorized");
                    return;
                }
            }

            // API key
            if (!File.Exists("api.cfg"))
            {
                Console.WriteLine("API key not set.");
                showHelp();
                return;
            }
            var trello = new Trello(File.ReadAllText("api.cfg"));
            
            // Boad id
            if (!File.Exists("boardid.cfg"))
            {
                Console.WriteLine("Board id not set.");
                showHelp();
                return;
            }
            string boardid = File.ReadAllText("boardid.cfg");

            // Auth
            if (!File.Exists("auth.cfg"))
            {
                Console.WriteLine("Not authorized.");
                showHelp();
                return;
            }
            trello.Authorize(File.ReadAllText("auth.cfg"));


            var glue = new TrelloTargetProcessGlueEngine(trello, boardid);
            glue.Run();

            Console.WriteLine("Done");
        }
    }

    internal class TrelloTargetProcessGlueEngine
    {
        private ITrello _trello;
        private readonly string _boardid;

        public TrelloTargetProcessGlueEngine(Trello trello, string boardid)
        {
            _trello = trello;
            _boardid = boardid;
        }

        public void Run()
        {
            var board = _trello.Boards.WithId(_boardid);
            var lists = _trello.Lists.ForBoard(board);
            var list = lists.First();

            foreach (var card in _trello.Cards.ForList(list))
            {
                Console.WriteLine(card.Name);
                Console.WriteLine(card.Id);
                Console.WriteLine();
            }
        }
    }
}
