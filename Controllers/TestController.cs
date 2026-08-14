using EMISAPIS.DTOS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EMISAPIS.Controllers
{
    public class TestController : Controller
    {
        [AllowAnonymous]
        [HttpGet("test-students")]
        public IActionResult GetTestStudents()
        {
            var dummyUsers = new List<UserDTO>
    {
        new UserDTO
        {
            user_id = 1,
            user_name = "Test User One",
            e_mail_id = "test1@cgmsc.gov.in",
            password = "password123",
            user_type = "Admin",
            designation = "Manager",
            address = "Raipur, Chhattisgarh",
            location_id = 101
        },
        new UserDTO
        {
            user_id = 2,
            user_name = "Test User Two",
            e_mail_id = "test2@cgmsc.gov.in",
            password = "password123",
            user_type = "User",
            designation = "Officer",
            address = "Bhilai, Chhattisgarh",
            location_id = 102
        }
    };

            return Ok(dummyUsers);
        }
    }
}
