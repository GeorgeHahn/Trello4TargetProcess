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

        public TrelloTargetProcessGlueEngine(Trello trello, string boardid, TargetProcess tp, Program.Settings settings)
        {
            _trello = trello;
            _boardid = boardid;
            _tp = tp;
            _settings = settings;

            if (_settings.TargetProcessProject == null)
                _settings.TargetProcessProject = new TargetProcess.Project()
                {
                    Id = 167,
                    Name = "Misc"
                };
        }

        public void Run()
        {
            var board = _trello.Boards.WithId(_boardid);
            var lists = _trello.Lists.ForBoard(board).ToList();
            var tpStoriesFromTrello = _tp.GetEntitiesFromTrello().ToList();

            SyncTrelloArchiveStateToTPEntities(tpStoriesFromTrello);
            SyncTPEntityStateToTrelloCards(tpStoriesFromTrello);
            SyncNewTrelloCardsToTP(tpStoriesFromTrello, board);
            UpdateWIPListOnTrelloFromInProgressTasksInTP(lists);
        }

        private void UpdateWIPListOnTrelloFromInProgressTasksInTP(List<List> lists)
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
                }
                else
                {
                    // Sync doneness from Trello to TP
                    if (_trello.Cards.WithId(story.TrelloId).Closed == true)
                    {
                        story.EntityState = new TargetProcess.EntityState {Name = "Done", Id = 82};
                            // TODO: THIS IS WRONG, ID DEPENDS ON THE PROJECT
                        _tp.AddUpdateEntity(story);
                    }
                }
            }
        }

        private void SyncNewTrelloCardsToTP(List<TargetProcess.IEntity> tpStoriesFromTrello, Board board)
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
                var card = _trello.Cards.WithId(id);
                var newStory = new TargetProcess.IEntity();
                newStory.Id = null; // new card
                newStory.Name = card.Name;
                newStory.Description = card.Desc;
                newStory.TrelloId = card.Id;
                card.Labels.ForEach(label => newStory.Tags += label.Name + ",");
                newStory.Tags?.Remove(newStory.Tags.Length - 1); // trim last comma
                newStory.Project = _settings.TargetProcessProject;

                _tp.AddUpdateUserStory(newStory);
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
            }
        }

        private void SyncTPEntityStateToTrelloCards(List<TargetProcess.IEntity> tpStoriesFromTrello)
        {
            // Find cards that have been done'd in TP, archive them in Trello
            var tpDonedEntities = from x in tpStoriesFromTrello
                                  where x.EntityState.Name == "Done"
                                  select x;

            foreach (var userStory in tpDonedEntities)
            {
                var card = _trello.Cards.WithId(userStory.TrelloId);
                if (card != null && card.Closed != true)
                    _trello.Cards.Archive(new CardId(userStory.TrelloId));
            }
        }

        private void SyncTrelloArchiveStateToTPEntities(List<TargetProcess.IEntity> tpStoriesFromTrello)
        {
            // Find cards that have been archived in Trello, 'Done' them in TP
            foreach (var story in tpStoriesFromTrello)
            {
                var card = _trello.Cards.WithId(story.TrelloId);
                if (card != null && card.Closed)
                {
                    if (story.EntityState.Name != "Done")
                    {
                        story.EntityState = new TargetProcess.EntityState { Name = "Done", Id = 82 };
                        _tp.AddUpdateEntity(story);
                    }
                }
            }
        }
    }
}