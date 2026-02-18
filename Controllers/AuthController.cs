using EMISAPIS.DTOS;
using Microsoft.Data.SqlClient;

using Microsoft.AspNetCore.Mvc;

namespace EMISAPIS.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly string _connectionString;
        public AuthController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }
        // ✅ GET ALL
        [HttpGet]
        public async Task<IActionResult> GetStudents()
        {
            var users = new List<UserDTO>();

            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();

            using SqlCommand cmd = new SqlCommand("SELECT * FROM Users", con);
            using SqlDataReader reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {

                users.Add(new UserDTO
                {
                    //user_id = Convert.ToInt32(reader["user_id"]),
                    user_id = reader["user_id"] != DBNull.Value
                ? Convert.ToInt32(reader["user_id"]) : 0,

                    user_name = reader["user_name"].ToString(),
                    e_mail_id = reader["e_mail_id"].ToString(),
                    password = reader["password"].ToString(),
                    user_type = reader["user_type"].ToString(),
                    designation = reader["designation"].ToString(),
                    address = reader["address"].ToString(),
                    location_id = reader["location_id"] != DBNull.Value
                ? Convert.ToInt32(reader["location_id"]) : 0,
                    //location_id = Convert.ToInt32(reader["location_id"]),
        //            pmis = reader["pmis"] != DBNull.Value
        //? Convert.ToChar(reader["pmis"])
        //: '\0',

        //            hrms = reader["hrms"] != DBNull.Value
        //? Convert.ToChar(reader["hrms"])
        //: '\0',


                    //public int user_id { get; set; }
                    //public string? user_name { get; set; }
                    //public string? e_mail_id { get; set; }
                    //public string? password { get; set; }
                    //public string? user_type { get; set; }
                    //public string? designation { get; set; }
                    //public string? address { get; set; }
                    //public int? location_id { get; set; }
                    //public char pmis { get; set; }
                    //public char hrms { get; set; }
                    //public char ems { get; set; }
                    //public int? supplier_id { get; set; }
                    //public int? empid { get; set; }


                });
            }

            return Ok(users);
        }
        [HttpGet("{id}")]
        public async Task<IActionResult> GetStudent(int id)
        {
            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();

            string query = "SELECT * FROM Users WHERE user_id=@user_id";
            using SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@user_id", id);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                var users = new UserDTO
                {
                    user_id = reader["user_id"] != DBNull.Value
                ? Convert.ToInt32(reader["user_id"]) : 0,

                    user_name = reader["user_name"].ToString(),
                    e_mail_id = reader["e_mail_id"].ToString(),
                    password = reader["password"].ToString(),
                    user_type = reader["user_type"].ToString(),
                    designation = reader["designation"].ToString(),
                    address = reader["address"].ToString(),
                    location_id = reader["location_id"] != DBNull.Value
                ? Convert.ToInt32(reader["location_id"]) : 0,
                };

                return Ok(users);
            }

            return NotFound("Student not found");
        }
    }
}
