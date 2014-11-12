using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        public Story(Trello _trello, TargetProcess _tp) // Kinda nasty
        {
            this._trello = _trello;
            this._tp = _tp;
        }

        private const string IDStaging = "";
        private const string IDAccepted = "";
        private const string IDWIP = "";
        private const string IDBlocked = "";

        private const string TPStaging = "";
        private const string TPAccepted = "";
        private const string TPWIP = "";
        private const string TPBlocked = "";

        // TODO: Populate this from settings
        private Dictionary<string, State> trelloStateMap = new Dictionary<string, State>()
        {
            {IDStaging, State.Staging},
            {IDAccepted, State.Accepted},
            {IDWIP, State.WIP},
            {IDBlocked, State.Blocked}
        };
        private Dictionary<string, State> tpStateMap = new Dictionary<string, State>()
        {
            {TPStaging, State.Staging},
            {TPAccepted, State.Accepted},
            {TPWIP, State.WIP},
            {TPBlocked, State.Blocked}
        };

        public void Update()
        {
            // NEED A WAY TO GET ARCHIVED TRELLO CARDS

            // TODO: Sync due dates

            if (TrelloCard.DateLastActivity > TargetProcessEntity.ModifyDate)
            {
                // Trello is newer
                SetName(TrelloCard.Name);
                SetDescription(TrelloCard.Desc);

                if (TrelloCard.Closed)
                    SetState(State.Done);
                else
                    SetState(trelloStateMap[TrelloCard.IdList]);

                //SetDueDate()

                var tags = TrelloCard.Labels.Aggregate((a, b) =>
                {
                    var label = new Card.Label();
                    label.Name = a.Name + "," + b.Name;
                    return label;
                });

                SetTags(tags.Name);
            }
            else
            {
                // TP is newer
                SetName(TargetProcessEntity.Name);
                SetDescription(TargetProcessEntity.Description);

                SetState(tpStateMap[TargetProcessEntity.EntityState.Name]);

                // Sync due date

                var tags = TrelloCard.Labels.Aggregate((a, b) =>
                {
                    var label = new Card.Label();
                    label.Name = a.Name + "," + b.Name;
                    return label;
                });

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

        private void SetDescription(string description)
        {
            if (TrelloCard.Desc != description)
            {
                TrelloCard.Desc = description;
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
            // TODO

            if (TrelloCard.Closed != done)
            {
                TrelloCard.Closed = done;
                TrelloStale = true;
            }

            if (TargetProcessEntity.EntityState.IsFinal != done)
            {
                TargetProcessEntity.EntityState = new TargetProcess.EntityState { Name = "Done", Id = 82 }; // TODO THIS IS STILL WRONG
                TPStale = true;
            }
        }
    }
}
