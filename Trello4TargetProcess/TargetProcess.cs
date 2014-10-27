using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Newtonsoft.Json;
using RestSharp;
using HttpUtils = ServiceStack.HttpUtils;

namespace Trello4TargetProcess
{
    // TODOs:
    //  Model cleanup
    //  Rip out RestSharp, replace with ServiceStack HttpUtils

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

        public void AddUpdateEntity(IEntity entity)
        {
            if(entity.EntityType.Name == "Task")
                AddUpdateTask(entity);
            else if(entity.EntityType.Name == "UserStory")
                AddUpdateUserStory(entity);
        }

        private void AddUpdateEntity(string endpoint, IEntity entity)
        {
            var api = _apiurl + endpoint;

            var storystr = JsonConvert.SerializeObject(entity, Formatting.None, new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore });

            HttpUtils.PostStringToUrl(HttpUtils.AddQueryParam(api, "token", _token), storystr, "application/json");
        }

        public void AddUpdateTask(IEntity entity)
        {
            AddUpdateEntity("Tasks/", entity);
        }

        public void AddUpdateUserStory(IEntity entity)
        {
            AddUpdateEntity("UserStories/", entity);
        }

        public IEnumerable<IEntity> GetUserStories()
        {
            var req = new RestRequest("UserStories/");
            req.RequestFormat = DataFormat.Json;
            req.AddHeader("Accept", "application/json");
            req.AddParameter("take", "1000"); // TODO: This will break miserable when we hit an inst with over 1000 entities
            var resp = client.Execute(req);
            var data = JsonConvert.DeserializeObject<EntityResponse>(resp.Content);
            return data.Items;
        }

        public IEnumerable<IEntity> GetUserStoriesInProgress()
        {
            var req = new RestRequest("UserStories/");
            req.RequestFormat = DataFormat.Json;
            req.AddHeader("Accept", "application/json");
            req.AddParameter("take", "1000"); // TODO: This will break miserable when we hit an inst with over 1000 entities

            req.AddParameter("where", "EntityState.Name eq 'In Progress'");

            var resp = client.Execute(req);
            var data = JsonConvert.DeserializeObject<EntityResponse>(resp.Content);
            return data.Items;
        }

        public IEnumerable<IEntity> GetTasksInProgress()
        {
            var req = new RestRequest("Tasks/");
            req.RequestFormat = DataFormat.Json;
            req.AddHeader("Accept", "application/json");
            req.AddParameter("take", "1000"); // TODO: This will break miserable when we hit an inst with over 1000 entities

            req.AddParameter("where", "EntityState.Name eq 'In Progress'");

            var resp = client.Execute(req);
            var data = JsonConvert.DeserializeObject<EntityResponse>(resp.Content);
            return data.Items;
        }

        public IEnumerable<IEntity> GetEntitiesFromTrello()
        {
            var entities = new List<IEntity>();
            entities.AddRange(GetUserStoriesFromTrello());
            entities.AddRange(GetTasksFromTrello());
            return entities;
        }

        public IEnumerable<IEntity> GetUserStoriesFromTrello()
        {
            var req = new RestRequest("UserStories/");
            req.RequestFormat = DataFormat.Json;
            req.AddHeader("Accept", "application/json");
            req.AddParameter("take", "1000"); // TODO: This will break miserable when we hit an inst with over 1000 entities

            // Custom field TrelloId
            req.AddParameter("where", "CustomFields.TrelloId is not null");

            var resp = client.Execute(req);
            var data = JsonConvert.DeserializeObject<EntityResponse>(resp.Content);
            return data.Items;
        }

        public IEnumerable<IEntity> GetTasksFromTrello()
        {
            var req = new RestRequest("Tasks/");
            req.RequestFormat = DataFormat.Json;
            req.AddHeader("Accept", "application/json");
            req.AddParameter("take", "1000"); // TODO: This will break miserable when we hit an inst with over 1000 entities

            // Custom field TrelloId
            req.AddParameter("where", "CustomFields.TrelloId is not null");

            var resp = client.Execute(req);
            var data = JsonConvert.DeserializeObject<EntityResponse>(resp.Content);
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

        public class EntityResponse
        {
            public string Next;
            public List<IEntity> Items;
        }

        public class IEntity
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

                    var fields = CustomFields.Where(f => f.Name == "TrelloId");
                    var field = fields.FirstOrDefault();

                    // Already exists
                    if (field != null)
                    {
                        field.Type = "Text";
                        field.Value = value;
                        return;
                    }

                    // Didn't exist
                    var newField = new CustomField
                    {
                        Name = "TrelloId",
                        Type = "Text",
                        Value = value
                    };
                    CustomFields.Add(newField);
                }
            }

            public DateTime ModifyDate;
            public EntityState EntityState;
            public List<CustomField> CustomFields;
            public EntityType EntityType { get; set; }
            public Project Project { get; set; }
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
            public bool? IsInitial;
            public bool? IsFinal;
            public double? NumericPriority;
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