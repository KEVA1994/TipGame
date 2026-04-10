using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace TipGame.Domain.Entities
{
    [Table("Users")]
    public class User : BaseModel
    {
        [PrimaryKey("Id")]
        public int Id { get; set; }

        [Column("Name")]
        public string Name { get; set; }

        // Not mapped — populated manually
        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public ICollection<Prediction> Predictions { get; set; } = new List<Prediction>();
    }
}
