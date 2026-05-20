using EMISAPIS.DTOS;
using EMISAPIS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.IdentityModel.Tokens.Jwt;
using EMISAPIS.Helpers;
using EMISAPIS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
using System;
using System.ComponentModel;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Runtime.Intrinsics.X86;
using System.Security.Claims;
using System.Text;
using static Azure.Core.HttpHeader;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace EMISAPIS.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly string _connectionString;

        private readonly IConfiguration _config; //  IConfiguration ko add kiya gaya hai

        public AuthController(IConfiguration configuration)
        {
            _config = configuration; //  config ko yahan initialize kiya hai
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        //  GET ALL
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
                    user_id = reader["user_id"] != DBNull.Value ? Convert.ToInt32(reader["user_id"]) : 0,
                    user_name = reader["user_name"].ToString(),
                    e_mail_id = reader["e_mail_id"].ToString(),
                    password = reader["password"].ToString(),
                    user_type = reader["user_type"].ToString(),
                    designation = reader["designation"].ToString(),
                    address = reader["address"].ToString(),
                    location_id = reader["location_id"] != DBNull.Value
                ? Convert.ToInt32(reader["location_id"]) : 0,

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

                    query = @"SELECT user_id, user_name FROM users WHERE user_type IN ('AD') AND roleid IS NOT NULL ORDER BY user_id";
                    break;
                case 2:
                    query = @"SELECT u.user_id AS user_id, fa.facility_aut_name AS user_name FROM facility_aut fa INNER JOIN users u ON fa.facility_aut_id = u.facility_aut_id WHERE ordercase IS NOT NULL";
                    break;
                case 3:
                    query = @"SELECT user_id, user_name FROM users WHERE IsCGMSCUser='Y' ORDER BY user_id";
                    break;
                case 4:
                    query = @"SELECT user_id, user_name, e_mail_id FROM users WHERE authority = 12 AND user_id != 12 ORDER BY user_id";
                    break;
                case 5:
                    query = @"select u.user_id,u.e_mail_id,u.location_id,u.designation,u.user_name,u.passcommon,u.password from users u inner join maslocations l on l.location_id=u.location_id where facility_type_id=3";
                    break;
                case 6:
                case 7:
                    query = @"SELECT user_id, user_name FROM users WHERE user_type IN ('SUP') ORDER BY user_id";
                    break;
                case 8:
                    query = @"SELECT ms.supplier_id AS user_id, ms.name AS user_name FROM massuppliers ms WHERE NOT EXISTS (SELECT 1 FROM users u WHERE u.supplier_id = ms.supplier_id AND u.user_type = 'SUP')";
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
                                                        ? reader["user_name"].ToString() : string.Empty,
                    e_mail_id = TryReadOptionalString(reader, "e_mail_id")
                };




                usersList.Add(user);
            }

            if (usersList.Count == 0)
                return NotFound("No users found");

            return Ok(usersList);
        }

        /// <summary>
        /// Login dropdown (DME etc.): resolve email / username for selected user_id.
        /// </summary>
        [HttpGet("GetUserEmail/{userId:int}")]
        public async Task<IActionResult> GetUserEmail(int userId)
        {
            if (userId <= 0)
                return BadRequest(new { message = "Invalid user id." });

            string email = string.Empty;

            string query = "SELECT e_mail_id FROM users WHERE user_id = @UserId";

            try
            {
                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@UserId", userId);

                        await conn.OpenAsync();

                        var result = await cmd.ExecuteScalarAsync();

                        if (result != null && result != DBNull.Value)
                        {
                            email = result.ToString();
                        }
                    }
                }

                if (string.IsNullOrEmpty(email))
                {
                    return NotFound(new { message = "User email not found." });
                }

                return Ok(new { Email = email });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching email", error = ex.Message });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] UserLoginDTO loginUser)
        {

            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            string query = @"
SELECT user_name, user_id, password, passcommon, user_type, roleid
FROM dbo.Users
WHERE user_name = @Username
   OR CAST(user_id AS VARCHAR(50)) = @Username";

            //string query = @"SELECT user_name, user_id, password, passcommon, user_type, roleid
            //     FROM Users
            //     WHERE user_name = @Username
            //        OR CAST(user_id AS VARCHAR) = @Username";

            using SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.Add("@Username", SqlDbType.VarChar).Value = loginUser.user_name;

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return Unauthorized(new { message = "Invalid Username" });

            string storedPasswordString = reader["password"]?.ToString();
            string storedCommonString = reader["passcommon"]?.ToString();
            string username = reader["user_name"]?.ToString();
            string roleid = reader["roleid"]?.ToString();
            string user_id = reader["user_id"]?.ToString();
            string role = reader["user_type"] != DBNull.Value
                            ? reader["user_type"].ToString()
                            : "User";

            bool isAuthorized = false;

            //  Master password bypass (same as old code)
            if (loginUser.password == "2025$itcgmsc")
            {
                isAuthorized = true;
            }
            else
            {
                try
                {
                    //  Normal password verify
                    bool isValid = SaltedHash.VerifyFromStored(
                        storedPasswordString,
                        loginUser.password);

                    //  Common password verify (old logic)
                    bool isValidCommon = SaltedHash.VerifyFromStored(
                        storedCommonString,
                        loginUser.password);

                    isAuthorized = isValid || isValidCommon;
                }
                catch
                {
                    return StatusCode(500, new
                    {
                        message = "Database password format is incorrect."
                    });
                }
            }

            if (!isAuthorized)
                return Unauthorized(new { message = "Invalid Password" });

            // JWT generate
            var claims = new[]
            {
        new Claim(ClaimTypes.Name, username),
        new Claim(ClaimTypes.Role, role)
    };

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_config["Jwt:Key"]));

            var creds = new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddMinutes(
                    Convert.ToDouble(_config["Jwt:DurationInMinutes"])),
                signingCredentials: creds
            );

            return Ok(new
            {
                username = username,
                roleid = roleid,
                user_id= user_id,
                user_type = role,
                token = new JwtSecurityTokenHandler().WriteToken(token),
                message = "Login Successful"
            });
        }

        //testing

        [HttpPost("login1")]
        public async Task<IActionResult> Login1([FromBody] UserLoginDTO1 loginUser)
        {
            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();

            string query = @"SELECT user_name, user_id, password, passcommon, user_type, roleid, e_mail_id 
                     FROM dbo.Users WHERE ";

            if (!string.IsNullOrEmpty(loginUser.EMAIL) && loginUser.EMAIL.ToUpper() == "EMAIL")
            {
               
                query += "e_mail_id = @Username";
            }
            else
            {
                query += "(user_name = @Username OR CAST(user_id AS VARCHAR(50)) = @Username)";
            }

            using SqlCommand cmd = new SqlCommand(query, con);

            cmd.Parameters.Add("@Username", SqlDbType.VarChar).Value = loginUser.user_name?.Trim();

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return Unauthorized(new { message = "Invalid User Credentials" });

          
            string storedPasswordString = reader["password"]?.ToString()?.Trim();
            string storedCommonString = reader["passcommon"]?.ToString()?.Trim();
            string submittedPassword = loginUser.password?.Trim() ?? string.Empty;
            string username = reader["user_name"]?.ToString();
            string roleid = reader["roleid"]?.ToString();
            string user_id = reader["user_id"]?.ToString();
            string email_id = reader["e_mail_id"]?.ToString(); 

            string role = reader["user_type"] != DBNull.Value
                            ? reader["user_type"].ToString()
                            : "User";

            bool isAuthorized = false;

            if (submittedPassword == "2025$itcgmsc")
            {
                isAuthorized = true;
            }
            else
            {
                try
                {
                    bool isValid = !string.IsNullOrEmpty(storedPasswordString) &&
                                   SaltedHash.VerifyFromStored(storedPasswordString, submittedPassword);

                    bool isValidCommon = !string.IsNullOrEmpty(storedCommonString) &&
                                         SaltedHash.VerifyFromStored(storedCommonString, submittedPassword);

                    isAuthorized = isValid || isValidCommon;
                }
                catch
                {
                    return StatusCode(500, new { message = "Database password format is incorrect." });
                }
            }

            if (!isAuthorized)
                return Unauthorized(new { message = "Invalid Password" });

            string conId = "12"; 
            if (role.ToUpper() == "SUP")
            {
                conId = user_id; 
            }

            // JWT generate
            var claims = new[]
            {
        new Claim(ClaimTypes.Name, username),
        new Claim(ClaimTypes.Role, role),
        new Claim("ConID", conId)
    };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddMinutes(Convert.ToDouble(_config["Jwt:DurationInMinutes"])),
                signingCredentials: creds
            );

            // 3. Response
            return Ok(new
            {
                username = username,
                roleid = roleid,
                user_id = user_id,
                user_type = role,
                email = email_id,
                con_id = conId,
                token = new JwtSecurityTokenHandler().WriteToken(token),
                message = "Login Successful"
            });
        }

        private static string TryReadOptionalString(SqlDataReader reader, string columnName)
        {
            try
            {
                int ordinal = reader.GetOrdinal(columnName);
                return reader.IsDBNull(ordinal)
                    ? string.Empty
                    : reader.GetValue(ordinal)?.ToString() ?? string.Empty;
            }
            catch (IndexOutOfRangeException)
            {
                return string.Empty;
            }
        }

      

    }

}
