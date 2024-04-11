namespace MedChat
{
    public static class Settings
    {
        public static string Language { get; set; }
        public static int nBest { get; set; }
        public static bool SupportNoteDraft { get; set; }

        public static float PenaltyScore { get; set; }
        public static HashSet<string> Hotfix { get; set;}

        public static ChatModel ChatModel { get; set; }
        public static BlobLogs BlobLogs { get; set; }

        public static string PatientTag { get; set; } = "[patient]";
        public static string DoctorTag { get; set; } = "[doctor]";
    
    }
}
