namespace EMISAPIS.DTOS
{
    public class UserLoginDTO
    {
        public string user_name { get; set; }
        public string password { get; set; }
    }
    public class UserLoginDTO1
    {
        public string user_name { get; set; }
        public string password { get; set; }
        public string EMAIL { get; set; } // <--- नया फ्लैग प्रॉपर्टी
    }
    
}
