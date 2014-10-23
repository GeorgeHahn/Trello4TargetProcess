using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using RestSharp;
using TrelloNet;
using System.Xml.Serialization;
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

            if (args.Length > 0)
            {
                var param = args[0];
                var val = args[1];

                if (param.Contains("help"))
                {
                    showHelp();
                    return;
                }

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

            var glue = new TrelloTargetProcessGlueEngine(trello, settings.TrelloBoardID, tpclient);
            glue.Run();

            Console.WriteLine("Done");
            Console.ReadLine();
        }

        class Settings
        {
            public string TrelloAppKey;
            public string TrelloAuthKey;
            public string TrelloBoardID;

            public string TargetProcessURL;
            public string TargetProcessToken;
            public string TargetProcessProject;
        }
    }

    internal class TrelloTargetProcessGlueEngine
    {
        private readonly ITrello _trello;
        private readonly string _boardid;
        private readonly TargetProcess _tp;

        public TrelloTargetProcessGlueEngine(Trello trello, string boardid, TargetProcess tp)
        {
            _trello = trello;
            _boardid = boardid;
            _tp = tp;
        }

        public void Run()
        {
            var board = _trello.Boards.WithId(_boardid);
            var lists = _trello.Lists.ForBoard(board);
            var list = lists.First();
            var trelloCards = _trello.Cards.ForList(list);

            var tpStoriesFromTrello = _tp.GetUserStoriesFromTrello();

            // Find cards that have been archived in Trello, 'Done' them in TP
            foreach (var story in tpStoriesFromTrello)
            {
                if(_trello.Cards.WithId(story.TrelloId).Closed)
                    // TODO: 'Done' card in TP
                    Console.WriteLine("Has been closed in trello");
            }

            // Find cards that have been done'd in TP, archive them in Trello
            var tpDonedEntities = from x in tpStoriesFromTrello
                           where x.entitystate.Name == "Done"
                           select x;

            foreach (var userStorey in tpDonedEntities)
            {
                var card = _trello.Cards.WithId(userStorey.TrelloId);
                if (card.Closed != true)
                    card.Closed = true;
            }


            // Find cards in Trello that aren't in TP, add them

            var tpIds = tpStoriesFromTrello.Select(tpEntity => tpEntity.TrelloId).ToList();
            var trelIds = _trello.Cards.ForList(list).Select(card => card.Id).ToList();

            for (int i = 0; i < tpIds.Count; i++)
            {
                if (trelIds.Contains(tpIds[i]))
                {
                    trelIds.Remove(tpIds[i]);
                    tpIds.Remove(tpIds[i]);
                    i--;
                }
            }

            // trelIds holds IDs of cards that are in Trello, but not in TP
            foreach (var id in trelIds)
            {
                var card = _trello.Cards.WithId(id);
                //_tp.AddNewUserStory(card.Name, etc etc);
            }

            // tpIds holds IDs of cards that are in TP, but not in Trello
            // When would this happen?
            // Trello -> card deleted
            // Trello -> card archived?
            // What do we do about it? Make a new card in trello and move from TP to Trello?


            // Take WIP cards from TP
            var wip = _tp.GetUserStoriesInProgress();
            foreach (var story in wip)
            {
                if (story.TrelloId == null)
                {
                    //AddToTrello(lists.ElementAt(1), story.Name etc etc);
                    Console.WriteLine("AddToTrello");
                }
                else
                {
                    // Sync doneness from Trello to TP
                    if (_trello.Cards.WithId(story.TrelloId).Closed == true)
                        //Mark entity's doneness
                        Console.WriteLine("Has been closed in trello");
                }
            }
        }
    }

    public class TargetProcess
    {
        private readonly string _token;
        private readonly RestClient client;

        public TargetProcess(string url, string token)
        {
            var _url = "https://" + url + "/api/v1/";
            _token = token;

            client = new RestClient(_url);
            client.Authenticator = new TokenAuthenticator(_token);
        }

        public IEnumerable<UserStory> GetUserStories()
        {
            var req = new RestRequest("UserStories/");
            req.RequestFormat = DataFormat.Json;
            req.AddHeader("Accept", "application/json");
            req.AddParameter("take", "1000"); // TODO: This will break miserable when we hit an inst with over 1000 entities
            var resp = client.Execute(req);
            var data = JsonConvert.DeserializeObject<UserStoryResponse>(resp.Content);
            return data.Items;
        }

        public IEnumerable<UserStory> GetUserStoriesInProgress()
        {
            var req = new RestRequest("UserStories/");
            req.RequestFormat = DataFormat.Json;
            req.AddHeader("Accept", "application/json");
            req.AddParameter("take", "1000"); // TODO: This will break miserable when we hit an inst with over 1000 entities

            req.AddParameter("where", "EntityState.Name eq 'In Progress'");

            var resp = client.Execute(req);
            var data = JsonConvert.DeserializeObject<UserStoryResponse>(resp.Content);
            return data.Items;
        }

        public IEnumerable<UserStory> GetUserStoriesFromTrello()
        {
            var req = new RestRequest("UserStories/");
            req.RequestFormat = DataFormat.Json;
            req.AddHeader("Accept", "application/json");
            req.AddParameter("take", "1000"); // TODO: This will break miserable when we hit an inst with over 1000 entities

            // Custom field TrelloId
            req.AddParameter("where", "CustomFields.TrelloId is not null");

            var resp = client.Execute(req);
            var data = JsonConvert.DeserializeObject<UserStoryResponse>(resp.Content);
            return data.Items;
        }

        public class TokenAuthenticator : IAuthenticator
        {
            private readonly string _token;
            public TokenAuthenticator(string token)
            {
                _token = token;
            }
            public void Authenticate(IRestClient client, IRestRequest request)
            {
                if (!request.Parameters.Any(p => p.Name.Equals("token", StringComparison.OrdinalIgnoreCase)))
                {
                    request.AddParameter("token", _token, ParameterType.GetOrPost);
                }
            }
        }

        public class UserStoryResponse
        {
            public string Next;
            public List<UserStory> Items;
        }

        public class UserStory
        {
            public Int32 Id;
            public string Name;
            public string Description;
            public string Tags;

            public string TrelloId
            {
                get
                {
                    if (CustomFields == null)
                        return null;

                    return (string) ((from field in CustomFields
                        where field.Name == "TrelloId"
                        select field.value).FirstOrDefault());
                }
            }

            public EntityState entitystate;

            public double NumericPriority;

            public List<CustomField> CustomFields;
        }

        public class CustomField
        {
            public string Name;
            public string Type;
            public object value;
        }

        public class EntityState
        {
            public Int32 Id;
            public string Name;
            public bool IsInitial;
            public bool IsFinal;
            public double NumericPriority;
        }

        public class Project
        {
            public Int32 Id;
            public string Name;
        }
    }
}
