using System;
using System.Collections.Generic;
using System.Text;

namespace TipGame.Domain.Entities
{
    public class Prediction
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public User User { get; set; }

        public int MatchId { get; set; }
        public Match Match { get; set; }

        public int PredictedHome { get; set; }
        public int PredictedAway { get; set; }

        public int Points { get; set; }
    }
}
