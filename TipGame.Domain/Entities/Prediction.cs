using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace TipGame.Domain.Entities
{
    [Table("Predictions")]
    public class Prediction : BaseModel
    {
        [PrimaryKey("Id")]
        public int Id { get; set; }

        [Column("UserId")]
        public int UserId { get; set; }

        [Column("MatchId")]
        public int MatchId { get; set; }

        [Column("PredictedHome")]
        public int PredictedHome { get; set; }

        [Column("PredictedAway")]
        public int PredictedAway { get; set; }

        [Column("Points")]
        public int Points { get; set; }

        // Not mapped — populated manually
        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public User User { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public Match Match { get; set; }
    }
}
