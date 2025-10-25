namespace emotions_gateway.Models;

public class Emotions
{
    public Guid id { get; set; }
    public Guid user_id { get; set; }
    public string modality { get; set; }  // ex: video, audio
    public string emotion { get; set; }   // ex: happy, sad
    public float confidence { get; set; }     // ex: 0.95
    public DateTime timestamp { get; set; }

}
