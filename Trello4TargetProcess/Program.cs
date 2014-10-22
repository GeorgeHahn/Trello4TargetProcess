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
            if (args.Length <= 1)
            {
                Console.WriteLine("Must be caleed with an application key");
                Console.WriteLine("Get yours at https://trello.com/1/appKey/generate");
                Console.WriteLine();
                Console.WriteLine("Usage:");
                Console.WriteLine("\tRequest authorization:\n\t\t Trello4TargetProcess [appkey] getauth");
                Console.WriteLine("\tAuthorize:\n\t\t Trello4TargetProcess [appkey] auth [authkey]");
                Console.WriteLine("\tRun:\n\t\t Trello4TargetProcess [appkey] board [boardid]");
                return;
            }

            var trello = new Trello(args[0]);
            string boardid;

            if (args[1] == "getauth")
            {
                var authurl = trello.GetAuthorizationUrl("Trello4TargetProcess", Scope.ReadWrite, Expiration.Never);
                Console.WriteLine("Authorization URL recieved");
                Console.WriteLine(authurl);
                return;
            }

            if (args[1] == "auth")
            {
                File.WriteAllText("auth.cfg", args[2]);
                trello.Authorize(args[2]);
                Console.WriteLine("Successfully authorized");
                return;
            }

            if (args[1] == "board")
            {
                boardid = args[2];
            }
            else
            {
                return;
            }

            if(!File.Exists("auth.cfg"))
                Console.WriteLine("Not authorized. Run with no arguments to see usage info.");

            trello.Authorize(File.ReadAllText("auth.cfg"));


            var board = trello.Boards.WithId(boardid);
            var lists = trello.Lists.ForBoard(board);
            var list = lists.First();

            foreach (var card in trello.Cards.ForList(list))
            {
                Console.WriteLine(card.Name);
                Console.WriteLine(card.Id);
                Console.WriteLine();
            }

            Console.ReadLine();
        }
    }
}
