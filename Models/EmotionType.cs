using System.ComponentModel.DataAnnotations.Schema;
namespace emotions_gateway.Models
{
    [Table("emotion_types")]
    public class EmotionsType
    {
        public string id { get; set; }
        public string name { get; set; } // ex: "happy", "sad"
        public string description { get; set; } // ex: "A feeling of joy or pleasure"
    }
}
