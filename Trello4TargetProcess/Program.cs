using System;
using System.Collections.Generic;
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
            if (args.Length < 1)
            {
                Console.WriteLine("Must be caleed with an application key");
                Console.WriteLine("Get yours at https://trello.com/1/appKey/generate");
                return;
            }

            var trello = new Trello(args[0]);

            if (args.Length > 1)
            {
                if (args[1].Contains("getauth"))
                {
                    var authurl = trello.GetAuthorizationUrl("Trello4TargetProcess", Scope.ReadWrite, Expiration.Never);
                    Console.WriteLine("Authorization URL recieved");
                    Console.WriteLine(authurl);
                }
                else if (args[1].Contains("auth"))
                {
                    trello.Authorize(args[2]);
                }
            }


        }
    }
}
