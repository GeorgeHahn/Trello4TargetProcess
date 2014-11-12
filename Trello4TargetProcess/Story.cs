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

        // TODO Need a generic way to track and update state:
        //  - Accepted (Don't show on Trello) = Pail list?
        //      - Want to be able to add on Trello, but then want them to disappear
        //      - Move to backing board?
        //      - Delete from Trello after adding to TP (will lose comments)
        //  - WIP (Show on WIP list)
        //  - Blocked (Show on Blocked list)
        //  - Done (Archive on Trello)

        public Story(Trello _trello, TargetProcess _tp) // Kinda nasty
        {
            this._trello = _trello;
            this._tp = _tp;
        }

        public void Update()
        {
            // Sync TrelloCard to TPE

            // NEED A WAY TO GET ARCHIVED TRELLO CARDS

            // if(TrelloIsNewer)
            {
                SetName(TrelloCard.Name);
                SetDescription(TrelloCard.Desc);
                SetDone(TrelloCard.Closed);

                var tags = TrelloCard.Labels.Aggregate((a, b) =>
                {
                    var label = new Card.Label();
                    label.Name = a.Name + "," + b.Name;
                    return label;
                });

                SetTags(tags.Name);
            }
            // Update name, desc, state, tags

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

        private void SetDone(bool done)
        {
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
