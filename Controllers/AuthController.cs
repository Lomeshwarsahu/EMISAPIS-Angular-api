using EMISAPIS.DTOS;
using EMISAPIS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using static System.Runtime.InteropServices.JavaScript.JSType;

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
        public async Task<IActionResult> GetUserbyid(int id)
        {
            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();

            string query = "";
            switch (id)
            {
                case 1:
                    query = @"SELECT user_id, user_name 
                  FROM users 
                  WHERE user_type IN ('AD') 
                  AND roleid IS NOT NULL 
                  ORDER BY user_id";
                    break;

                case 2:
                    query = @"SELECT 
                    u.user_id AS user_id, 
                    fa.facility_aut_name AS user_name
                  FROM facility_aut fa
                  INNER JOIN users u 
                    ON fa.facility_aut_id = u.facility_aut_id
                  WHERE ordercase IS NOT NULL";
                    break;

                case 3:
                    query = @"SELECT user_id, user_name 
                  FROM users 
                  WHERE IsCGMSCUser='Y'  
                  ORDER BY user_id";
                    break;
                case 4:
                    query = @"SELECT user_id, user_name FROM users WHERE authority = 12 AND user_id != 12 ORDER BY user_id";

                    break;
                case 5:
                    query = @"select u.user_id,u.e_mail_id,u.location_id,u.designation,u.user_name,u.passcommon,u.password from users u 
                                inner join maslocations l on l.location_id=u.location_id where facility_type_id=3";

                    break;
                case 6:
                    query = @"SELECT user_id, user_name
                 FROM users
                 WHERE user_type IN ('SUP')
                 ORDER BY user_id";

                    break;
                case 7:
                    query = @"SELECT user_id, user_name
                 FROM users
                 WHERE user_type IN ('SUP')
                 ORDER BY user_id";

                    break;
                case 8:
                    query = @"SELECT 
    ms.supplier_id AS user_id,
    ms.name AS user_name
FROM massuppliers ms
WHERE NOT EXISTS
(
    SELECT 1 
    FROM users u
    WHERE u.supplier_id = ms.supplier_id
    AND u.user_type = 'SUP'
)";
                   

                    break;

                default:
                    return BadRequest("Invalid id");
            }


            using SqlCommand cmd = new SqlCommand(query, con);
            using SqlDataReader reader = await cmd.ExecuteReaderAsync();

            List<UserDTO> usersList = new List<UserDTO>();

            while (await reader.ReadAsync())
            {
                var user = new UserDTO
                {
                                            user_id = reader["user_id"] != DBNull.Value
                                                      ? Convert.ToInt32(reader["user_id"]) : 0,
                                            user_name = reader["user_name"] != DBNull.Value
                                                        ? reader["user_name"].ToString() : string.Empty
                };



                usersList.Add(user);
            }

            if (usersList.Count == 0)
                return NotFound("No users found");

            return Ok(usersList);
        }

    }
}
