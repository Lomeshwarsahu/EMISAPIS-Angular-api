
using System.Security.Cryptography;
using System.Text;
namespace EMISAPIS.Helpers
{
    public class SaltedHash
    {
        public static bool VerifyFromStored(string storedValue, string password)
        {
            if (string.IsNullOrWhiteSpace(storedValue))
                return false;

            const string mStart = "salt{";
            const string mMid = "}hash{";
            const string mEnd = "}";

            if (!storedValue.Contains(mStart) || !storedValue.Contains(mMid))
                return false;

            string salt = storedValue.Substring(
                storedValue.IndexOf(mStart) + mStart.Length,
                storedValue.IndexOf(mMid) -
                (storedValue.IndexOf(mStart) + mStart.Length));

            string hash = storedValue.Substring(
                storedValue.IndexOf(mMid) + mMid.Length,
                storedValue.LastIndexOf(mEnd) -
                (storedValue.IndexOf(mMid) + mMid.Length));
            Console.WriteLine("Stored: " + hash);
            string computedHash = ComputeHash(password, salt);
            Console.WriteLine("Computed: " + computedHash);
            return computedHash == hash;
        }
        private static string ComputeHash(string password, string salt)
        {
            using SHA1 sha1 = SHA1.Create(); 
            byte[] bytes = Encoding.UTF8.GetBytes(salt + password);

            byte[] hashBytes = sha1.ComputeHash(bytes);
          
            return Convert.ToBase64String(hashBytes);
        }
     
    }
}