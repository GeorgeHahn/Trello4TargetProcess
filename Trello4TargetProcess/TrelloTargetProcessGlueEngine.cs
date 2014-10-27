using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using TrelloNet;

namespace Trello4TargetProcess
{
    internal class TrelloTargetProcessGlueEngine
    {
        private readonly ITrello _trello;
        private readonly string _boardid;
        private readonly TargetProcess _tp;
        private readonly Program.Settings _settings;
        private readonly Board board;
        private readonly List<List> lists;
        private readonly Dictionary<string, Card> trelloCards;

        private System.Action<string> Log = s => Console.WriteLine(s);

        public TrelloTargetProcessGlueEngine(Trello trello, string boardid, TargetProcess tp, Program.Settings settings)
        {
            _trello = trello;
            _boardid = boardid;
            _tp = tp;
            _settings = settings;
            board = _trello.Boards.WithId(_boardid);
            lists = _trello.Lists.ForBoard(board).ToList();
            trelloCards = new Dictionary<string, Card>();

            if (_settings.TargetProcessProject == null)
                _settings.TargetProcessProject = new TargetProcess.Project()
                {
                    Id = 167,
                    Name = "Misc"
                };
        }

        public void Run()
        {
            var tpStoriesFromTrello = _tp.GetEntitiesFromTrello().ToList();

            // Update trelloCards dictionary with any new keys
            foreach (var id in tpStoriesFromTrello)
            {
                if(!trelloCards.ContainsKey(id.TrelloId))
                    trelloCards[id.TrelloId] = _trello.Cards.WithId(id.TrelloId);
            }

            SyncData(tpStoriesFromTrello, trelloCards);
            SyncTrelloArchiveStateToTPEntities(tpStoriesFromTrello, trelloCards);
            SyncTPEntityStateToTrelloCards(tpStoriesFromTrello, trelloCards);
            SyncNewTrelloCardsToTP(tpStoriesFromTrello, board, trelloCards);
            UpdateWIPListOnTrelloFromInProgressTasksInTP(lists, trelloCards);
        }

        private void SyncData(List<TargetProcess.IEntity> lists, Dictionary<string, Card> cards)
        {
            foreach (var entity in lists)
            {
                if (!string.IsNullOrWhiteSpace(entity.Description) &&
                    !string.IsNullOrWhiteSpace(cards[entity.TrelloId].Desc))
                {
                    if (entity.Description != cards[entity.TrelloId].Desc)
                    {
                        if (entity.ModifyDate > cards[entity.TrelloId].DateLastActivity)
                        {
                            cards[entity.TrelloId].Desc = entity.Description;
                            _trello.Cards.Update(cards[entity.TrelloId]);
                            Log("Updated Trello desc for: " + entity.Name);
                        }
                        else
                        {
                            entity.Description = cards[entity.TrelloId].Desc;
                            _tp.AddUpdateEntity(entity);
                            Log("Updated TP desc for: " + entity.Name);
                        }
                    }
                }


                if (!string.IsNullOrWhiteSpace(entity.Name) &&
                    !string.IsNullOrWhiteSpace(cards[entity.TrelloId].Name))
                {
                    if (entity.Name != cards[entity.TrelloId].Name)
                    {
                        if (entity.ModifyDate > cards[entity.TrelloId].DateLastActivity.ToLocalTime())
                        {
                            cards[entity.TrelloId].Name = entity.Name;
                            _trello.Cards.Update(cards[entity.TrelloId]);
                            Log("Updated Trello name for: " + entity.Name);
                        }
                        else
                        {
                            entity.Name = cards[entity.TrelloId].Name;
                            _tp.AddUpdateEntity(entity);
                            Log("Updated TP name for: " + entity.Name);
                        }
                    }
                }
            }
        }

        private void UpdateWIPListOnTrelloFromInProgressTasksInTP(List<List> lists, Dictionary<string, Card> trelloCards)
        {
            // Take WIP cards from TP and send them to trello
            var wip = new List<TargetProcess.IEntity>();
            wip.AddRange(_tp.GetUserStoriesInProgress());
            wip.AddRange(_tp.GetTasksInProgress());

            foreach (var story in wip)
            {
                if (story.TrelloId == null)
                {
                    var newCard = _trello.Cards.Add(story.Name, new ListId(lists.ElementAt(1).Id)); // WIP list
                    newCard.Desc = story.Description;

                    story.TrelloId = newCard.Id;
                    _tp.AddUpdateEntity(story);

                    Log("Added to Trello: " + story.Name);
                }
                else
                {
                    // Sync doneness from Trello to TP
                    if (trelloCards[story.TrelloId].Closed == true)
                    {
                        story.EntityState = new TargetProcess.EntityState {Name = "Done", Id = 82};
                            // TODO: THIS IS WRONG, ID DEPENDS ON THE PROJECT

                        _tp.AddUpdateEntity(story);

                        Log("Closed in TP: " + story.Name);
                    }
                }
            }
        }

        private void SyncNewTrelloCardsToTP(List<TargetProcess.IEntity> tpStoriesFromTrello, Board board, Dictionary<string, Card> trelloCards)
        {
            // Find cards in Trello that aren't in TP
            var tpIds = tpStoriesFromTrello.Select(tpEntity => tpEntity.TrelloId).ToList();
            var trelloIds = _trello.Cards.ForBoard(board).Select(card => card.Id).ToList();

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
                var card = trelloCards[id];
                var newStory = new TargetProcess.IEntity();
                newStory.Id = null; // new card
                newStory.Name = card.Name;
                newStory.Description = card.Desc;
                newStory.TrelloId = card.Id;
                card.Labels.ForEach(label => newStory.Tags += label.Name + ",");
                newStory.Tags?.Remove(newStory.Tags.Length - 1); // trim last comma
                newStory.Project = _settings.TargetProcessProject;

                _tp.AddUpdateUserStory(newStory);

                Log("Added to TP: " + newStory.Name);
            }

            ScrubTrelloIDsFromTP(tpStoriesFromTrello, tpIds);
        }

        private void ScrubTrelloIDsFromTP(List<TargetProcess.IEntity> tpStoriesFromTrello, List<string> tpIds)
        {
            // tpIds holds IDs of cards that are in TP, but not in Trello
            // When would this happen?
            // Trello -> card deleted?
            // What do we do about it? Make a new card in trello and move from TP to Trello?
            foreach (var badID in tpIds)
            {
                var tpentity = tpStoriesFromTrello.FirstOrDefault(entity => entity.TrelloId == badID);
                tpentity.TrelloId = null;
                _tp.AddUpdateEntity(tpentity);

                Log("Scrubbed old Trello ID from: " + tpentity.Name);
            }
        }

        private void SyncTPEntityStateToTrelloCards(List<TargetProcess.IEntity> tpStoriesFromTrello, Dictionary<string, Card> trelloCards)
        {
            // Find cards that have been done'd in TP, archive them in Trello
            var tpDonedEntities = from x in tpStoriesFromTrello
                                  where x.EntityState.Name == "Done"
                                  select x;

            foreach (var userStory in tpDonedEntities)
            {
                var card = trelloCards[userStory.TrelloId];
                if (card != null && card.Closed != true)
                {
                    _trello.Cards.Archive(new CardId(userStory.TrelloId));
                    Log("Archived card in Trello: " + userStory.Name);
                }
            }
        }

        private void SyncTrelloArchiveStateToTPEntities(List<TargetProcess.IEntity> tpStoriesFromTrello, Dictionary<string, Card> trelloCards)
        {
            // Find cards that have been archived in Trello, 'Done' them in TP
            foreach (var story in tpStoriesFromTrello)
            {
                var card = trelloCards[story.TrelloId];
                if (card != null && card.Closed)
                {
                    if (story.EntityState.Name != "Done")
                    {
                        story.EntityState = new TargetProcess.EntityState { Name = "Done", Id = 82 };
                        _tp.AddUpdateEntity(story);

                        Log("Doned card in TP: " + story.Name);
                    }
                }
            }
        }
    }
}