using System;
using System.Collections.Generic;
using System.Text;

namespace TipGame.Domain.Entities
{
    public class User
    {
        public int Id { get; set; }

        public string Name { get; set; }

        // GUID fra browser (localStorage)
        public string ClientId { get; set; }

        public ICollection<Prediction> Predictions { get; set; } = new List<Prediction>();
    }
}
