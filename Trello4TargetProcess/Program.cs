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
using ServiceStack;
using Formatting = Newtonsoft.Json.Formatting;
using IRestClient = RestSharp.IRestClient;

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
                           where x.EntityState.Name == "Done"
                           select x;

            foreach (var userStorey in tpDonedEntities)
            {
                var card = _trello.Cards.WithId(userStorey.TrelloId);
                if (card.Closed != true)
                    card.Closed = true;
            }

            // Find cards in Trello that aren't in TP, add them
            var tpIds = tpStoriesFromTrello.Select(tpEntity => tpEntity.TrelloId).ToList();
            var trelloIds = _trello.Cards.ForList(list).Select(card => card.Id).ToList();

            for (int i = 0; i < tpIds.Count; i++)
            {
                if (trelloIds.Contains(tpIds[i]))
                {
                    trelloIds.Remove(tpIds[i]);
                    tpIds.Remove(tpIds[i]);
                    i--;
                }
            }

            // IDs of cards that are in Trello, but not in TP
            foreach (var id in trelloIds)
            {
                var card = _trello.Cards.WithId(id);
                var newStory = new TargetProcess.UserStory();
                newStory.Name = card.Name;
                newStory.Description = card.Desc;
                newStory.TrelloId = card.Id;

                string bl = "";
                foreach (var va in card.Labels)
                {
                    bl = va.Name + ",";
                }

                newStory.Id = null; // Force new card (vs update or error)
                newStory.Tags = bl;
                newStory.Project = new TargetProcess.Project()
                {
                    Id = 167,
                    Name = "Misc"
                };

                _tp.AddUpdateUserStory(newStory);
            }

            // tpIds holds IDs of cards that are in TP, but not in Trello
            // When would this happen?
            // Trello -> card deleted
            // Trello -> card archived?
            // What do we do about it? Make a new card in trello and move from TP to Trello?


            // Take WIP cards from TP
            var wip = new List<TargetProcess.IEntity>();
            wip.AddRange(_tp.GetUserStoriesInProgress());
            wip.AddRange(_tp.GetTasksInProgress());

            foreach (var story in wip)
            {
                if (story.TrelloId == null)
                {
                    // TODO: Add entity to trello
                    // TODO: Add trello ID to entity

                    //AddToTrello(lists.ElementAt(1), story.Name etc etc);


                    Console.WriteLine("AddToTrello");
                }
                else
                {
                    // Sync doneness from Trello to TP
                    if (_trello.Cards.WithId(story.TrelloId).Closed == true)
                        // TODO Mark story as done in TP
                        Console.WriteLine("Has been closed in trello");
                }
            }
        }
    }

    public class TargetProcess
    {
        private readonly string _token;
        private readonly RestClient client;
        private readonly string _apiurl;

        public TargetProcess(string url, string token)
        {
            _apiurl = "https://" + url + "/api/v1/";
            _token = token;

            client = new RestClient(_apiurl);
            client.Authenticator = new TokenAuthenticator(_token);
        }

        public void AddUpdateUserStory(UserStory story)
        {
            var endpoint = _apiurl + "UserStories/";

            var storystr = JsonConvert.SerializeObject(story, Formatting.None, new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore });

            endpoint
                .AddQueryParam("token", _token)
                .PostStringToUrl(storystr, "application/json");
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

        public IEnumerable<Task> GetTasksInProgress()
        {
            var req = new RestRequest("Tasks/");
            req.RequestFormat = DataFormat.Json;
            req.AddHeader("Accept", "application/json");
            req.AddParameter("take", "1000"); // TODO: This will break miserable when we hit an inst with over 1000 entities

            req.AddParameter("where", "EntityState.Name eq 'In Progress'");

            var resp = client.Execute(req);
            var data = JsonConvert.DeserializeObject<TaskResponse>(resp.Content);
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

        public class TaskResponse
        {
            public string Next;
            public List<Task> Items;
        }

        public abstract class IEntity
        {
            public int? Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public string Tags { get; set; }

            [JsonIgnore]
            [XmlIgnore]
            public string TrelloId
            {
                get
                {
                    return (string) CustomFields?.Where(field => field.Name == "TrelloId")
                                                 .Select(field => field.Value)
                                                 .FirstOrDefault();
                }
                set
                {
                    if(CustomFields == null)
                        CustomFields = new List<CustomField>();

                    if (this.TrelloId == null)
                    {
                        var field = new CustomField
                        {
                            Name = "TrelloId",
                            Type = "Text",
                            Value = value
                        };
                        CustomFields.Add(field);
                    }
                    else
                    {
                        var fields = CustomFields.Where(f => f.Name == "TrelloId");
                        var field = fields.FirstOrDefault();
                        if (field != null)
                            field.Name = value;
                    }
                }
            }

            public EntityState EntityState;
            public List<CustomField> CustomFields;
        }

        public class UserStory : IEntity
        {
            public EntityType EntityType { get; set; }
            public Project Project { get; set; }
        }

        public class Task : IEntity
        {
        }

        public class CustomField
        {
            public string Name;
            public string Type;
            public object Value;
        }

        public class EntityState
        {
            public Int32 Id;
            public string Name;
            public bool IsInitial;
            public bool IsFinal;
            public double NumericPriority;
        }

        public class Owner
        {
            public int Id { get; set; }
            public string FirstName { get; set; }
            public string LastName { get; set; }
        }

        public class Priority
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        public class Project
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        public class EntityType
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }
    }
}
