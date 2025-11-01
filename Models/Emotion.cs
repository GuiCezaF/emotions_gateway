using System.ComponentModel.DataAnnotations.Schema;

namespace emotions_gateway.Models
{
    [Table("emotions")]
    public class Emotions
    {
        public Guid id { get; set; }
        public string user_id { get; set; }
        public string modality { get; set; }
        public decimal confidence { get; set; }
        public DateTime timestamp { get; set; }

        [Column("emotion_type_id")]
        public string emotion_type_id { get; set; }

        [ForeignKey("emotion_type_id")]
        public EmotionsType EmotionType { get; set; }
    }
}
