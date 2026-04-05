using System;
using System.Collections.Generic;
using System.Text;

namespace TipGame.Domain.Entities
{
    public class Match
    {
        public int Id { get; set; }

        // ID fra Football API
        public int ExternalId { get; set; }

        public string HomeTeam { get; set; }
        public string AwayTeam { get; set; }

        public int? HomeScore { get; set; }
        public int? AwayScore { get; set; }

        public DateTime KickoffTime { get; set; }

        // SCHEDULED, FINISHED osv.
        public string Status { get; set; }

        public ICollection<Prediction> Predictions { get; set; } = new List<Prediction>();
    }
}
