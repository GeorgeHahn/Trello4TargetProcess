using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;
using TrelloNet;

namespace Trello4TargetProcess
{
    public class Story
    {
        public Card TrelloCard { get; private set; }
        public TargetProcess.IEntity TargetProcessEntity { get; private set; }

        private readonly Trello _trello;
        private readonly TargetProcess _tp;

        private bool TrelloStale = false;
        private bool TPStale = false;

        private enum State
        {
            Staging,
            //      - Want to be able to add on Trello, but then want them to disappear
            //      - Move to backing board?
            //      - Delete from Trello after adding to TP (will lose comments)

            Accepted,
            WIP,
            Blocked,
            Done
        }

        // TODO: Populate this from settings
        private const string IDStaging = "";
        private const string IDAccepted = "";
        private const string IDWIP = "";
        private const string IDBlocked = "";

        private const int TPStaging = 0;
        private const int TPAccepted = 0;
        private const int TPWIP = 0;
        private const int TPBlocked = 0;

        private Dictionary<string, State> trelloStateMap = new Dictionary<string, State>()
        {
            {IDStaging, State.Staging},
            {IDAccepted, State.Accepted},
            {IDWIP, State.WIP},
            {IDBlocked, State.Blocked}
        };
        private Dictionary<int, State> tpStateMap = new Dictionary<int, State>()
        {
            {TPStaging, State.Staging},
            {TPAccepted, State.Accepted},
            {TPWIP, State.WIP},
            {TPBlocked, State.Blocked}
        };

        public Story(Trello _trello, TargetProcess _tp, Card trelloCard, TargetProcess.IEntity targetProcessEntity) // Kinda nasty
        {
            this._trello = _trello;
            this._tp = _tp;
            TargetProcessEntity = targetProcessEntity;
            TrelloCard = trelloCard;
        }

        public void Update()
        {
            // TODO: NEED A WAY TO GET ARCHIVED TRELLO CARDS
            // TODO: Sync due dates
            // TODO: Sync Trello checklists (to what? TP tasks?)

            if (TrelloCard.DateLastActivity.ToLocalTime() > TargetProcessEntity.ModifyDate.ToLocalTime())
            {
                // Trello is newer
                SetName(TrelloCard.Name);
                SetDescription(TrelloCard.Desc);
                SetState(TrelloCard.Closed ? State.Done : trelloStateMap[TrelloCard.IdList]);
                //SetDueDate()
                var tags = TrelloCard.Labels.Aggregate((a, b) => new Card.Label {Name = a.Name + "," + b.Name});
                SetTags(tags.Name);
            }
            else
            {
                // TP is newer
                SetName(TargetProcessEntity.Name);
                SetDescription(TargetProcessEntity.Description);
                SetState(tpStateMap[TargetProcessEntity.EntityState.Id]);
                //SetDueDate()
                var tags = TrelloCard.Labels.Aggregate((a, b) => new Card.Label { Name = a.Name + "," + b.Name });
                SetTags(tags.Name);
            }

            if (TrelloStale)
            {
                _trello.Cards.Update(TrelloCard);
                TrelloStale = false;
            }

            if (TPStale)
            {
                _tp.AddUpdateEntity(TargetProcessEntity);
                TPStale = false;
            }
        }

        private void SetName(string name)
        {
            if (TrelloCard.Name != name)
            {
                TrelloCard.Name = name;
                TrelloStale = true;
            }

            if (TargetProcessEntity.Name != name)
            {
                TargetProcessEntity.Name = name;
                TPStale = true;
            }
        }

        public static string DeHTML(string HTML)
        {
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(HTML);
            var text = "";
            foreach (var element in doc.DocumentNode.ChildNodes)
            {
                text += GetNodeText(element);
            }
            return text;
        }

        private static string GetNodeText(HtmlNode element)
        {
            var text = "";
            if (element.NodeType == HtmlNodeType.Text)
                text += HtmlEntity.DeEntitize(((HtmlTextNode)element).Text).Trim();
            else if (element.NodeType == HtmlNodeType.Element)
            {
                if (element.Name == "ul")
                    text += "- ";

                if(element.HasChildNodes)
                    foreach (var child in element.ChildNodes)
                        text += GetNodeText(child);

                if (element.Name == "div" || element.Name == "p")
                    text += Environment.NewLine;
            }
            return text;
        }

        private void SetDescription(string description)
        {
            // TODO Rich list sync
            // Trello lists use hyphen and numbers for lists
            // TP lists use html

            if (TrelloCard.Desc != description)
            {
                TrelloCard.Desc = DeHTML(description);
                TrelloStale = true;
            }

            if (TargetProcessEntity.Description != description)
            {
                TargetProcessEntity.Description = description;
                TPStale = true;
            }
        }

        private void SetTags(string tags)
        {
            //if (TrelloCard.Desc != tags) // TODO: Add a TrelloCard extension method that gives Trello tags as comma separated list
            //{
                // Figure out what labels are set in Trello
                // If any of these match, set it
                
                //TrelloStale = true;
            //}

            if (TargetProcessEntity.Tags != tags)
            {
                TargetProcessEntity.Tags = tags;
                TPStale = true;
            }
        }

        private void SetState(State state)
        {
            if (state == State.Done)
            {
                if (!TrelloCard.Closed)
                {
                    TrelloCard.Closed = true;
                    TrelloStale = true;
                }
            }
            else
            {
                if (TrelloCard.Closed)
                {
                    TrelloCard.Closed = false;
                    TrelloStale = true;
                }

                var listId = trelloStateMap.FirstOrDefault(x => x.Value == state).Key;
                if (TrelloCard.IdList != listId)
                {
                    TrelloCard.IdList = listId;
                    TrelloStale = true;
                }
            }

            var tpstate = tpStateMap.FirstOrDefault(x => x.Value == state).Key;
            if(TargetProcessEntity.EntityState.Id != tpstate)
            {
                TargetProcessEntity.EntityState = new TargetProcess.EntityState { Id = tpstate };
                TPStale = true;
            }
        }
    }
}
