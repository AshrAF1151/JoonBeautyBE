using JCOP.Models;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace JCOP.Controllers
{
    public class AuthenticationController : Controller
    {
        private readonly string _connectionString = @"Data Source=13.52.54.82,1433\EC2AMAZ-GLMLTJA;Initial Catalog=SparkDB;User ID=sa;Password=Pass123;";
        private readonly IConfiguration _configuration;

        public AuthenticationController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpPost]
        [Route("login")]
        public async Task<ActionResult> Login([FromBody] Login model)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (SqlCommand command = new SqlCommand("SELECT CustNum, Phone_Number, First_name, Last_name, EMail, Street_Address_1, Company_Name FROM Customer WHERE REPLACE(Phone_Number, '-', '') = @PhoneNumber", conn))
                    //AND EMail = @Email
                    {
                        string cleanedPhoneNumber = model.PhoneNumber.Replace("-", "");

                        command.Parameters.AddWithValue("@PhoneNumber", cleanedPhoneNumber);
                        //command.Parameters.AddWithValue("@Email", model.Email);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                var customer = new
                                {
                                    CustomerNumber = reader.GetString(0),
                                    PhoneNumber = reader.GetString(1),
                                    FirstName = reader.GetString(2),
                                    LastName = reader.GetString(3),
                                    Email = reader.GetString(4),
                                    StreetAddress = reader.GetString(5),
                                    CompanyName = reader.GetString(6)
                                };

                                var tokenHandler = new JwtSecurityTokenHandler();
                                var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]);
                                var tokenDescriptor = new SecurityTokenDescriptor
                                {
                                    Subject = new ClaimsIdentity(new[]
                                    {
                                        new Claim(ClaimTypes.NameIdentifier, customer.CustomerNumber),
                                        new Claim(ClaimTypes.Name, customer.FirstName),
                                        new Claim(ClaimTypes.Email, customer.Email)
                                    }),
                                    Expires = DateTime.UtcNow.AddHours(24),
                                    Issuer = _configuration["Jwt:Issuer"],
                                    Audience = _configuration["Jwt:Audience"],
                                    SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
                                };

                                var token = tokenHandler.CreateToken(tokenDescriptor);
                                var tokenString = tokenHandler.WriteToken(token);

                                var result = new
                                {
                                    success = true,
                                    message = "OK",
                                    token = tokenString,
                                    customer
                                };

                                return Ok(result);
                            }
                            else
                            {
                                return Ok(new { success = false, message = "Cannot process your request. Please contact your provider." });
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                return Ok(new { statusCode = "403", status = "Forbidden", message = e.Message });
            }
        }

        [HttpPost]
        [Route("register")]
        public async Task<ActionResult> Register([FromBody] RegisterUser model)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    string checkExistingUserQuery = @"SELECT COUNT(*) FROM Customer WHERE Email = @Email AND Phone_Number = @PhoneNumber";

                    using (SqlCommand checkExistingUserCmd = new SqlCommand(checkExistingUserQuery, conn))
                    {
                        checkExistingUserCmd.Parameters.AddWithValue("@Email", model.Email);
                        checkExistingUserCmd.Parameters.AddWithValue("@PhoneNumber", model.PhoneNumber);
                        //checkExistingUserCmd.Parameters.AddWithValue("@CompanyName", model.CompanyName);

                        int existingUserCount = (int)checkExistingUserCmd.ExecuteScalar();

                        // If the user already exists, return a message
                        if (existingUserCount > 0)
                        {
                            return Ok(new { success = false, message = "User already exists" });
                        }
                    }

                    string getCustNumQuery = "SELECT CustNum FROM Customer";

                    List<long> existingCustNums = new List<long>();

                    using (SqlCommand getCustNumCmd = new SqlCommand(getCustNumQuery, conn))
                    {
                        using (SqlDataReader reader = getCustNumCmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string custNumString = reader.GetString(0);
                                int custNum;
                                if (int.TryParse(custNumString, out custNum))
                                {
                                    existingCustNums.Add(custNum);
                                }
                                else
                                {
                                }
                            }
                        }
                    }

                    int initialCustNum = 50001;
                    while (existingCustNums.Contains(initialCustNum))
                    {
                        initialCustNum++;
                    }
                    int newCustNum = initialCustNum;
                    string insertQuery = @"INSERT INTO Customer (CustNum, Email, Phone_Number, First_name, Last_name) 
                             VALUES (@CustNum, @Email, @PhoneNumber, @FirstName, @LastName)";
                    using (SqlCommand command = new SqlCommand(insertQuery, conn))
                    {
                        command.Parameters.AddWithValue("@CustNum", newCustNum.ToString());
                        command.Parameters.AddWithValue("@Email", model.Email);
                        command.Parameters.AddWithValue("@PhoneNumber", model.PhoneNumber);
                        command.Parameters.AddWithValue("@FirstName", model.FirstName);
                        command.Parameters.AddWithValue("@LastName", model.LastName);
                        //command.Parameters.AddWithValue("@CompanyName", model.CompanyName);
                        //command.Parameters.AddWithValue("@StreetAddress", model.StreetAddress);
                        //command.Parameters.AddWithValue("@City", model.City);

                        int rowsAffected = command.ExecuteNonQuery();
                        if (rowsAffected > 0)
                        {
                            var newUser = new
                            {
                                CustNum = newCustNum,
                                Email = model.Email,
                                PhoneNumber = model.PhoneNumber,
                                FirstName = model.FirstName,
                                LastName = model.LastName
                            };

                            return Ok(new { success = true, message = "User registered successfully", user = newUser });
                        }
                        else
                        {
                            return Ok(new { success = false, message = "User registration failed" });
                        }
                    }
                }
            }
            catch (Exception e)
            {
                return Ok(new { statusCode = "500", status = "Internal Server Error", message = e.Message });
            }
        }

        [HttpPost]
        [Route("adminLogin")]
        public async Task<ActionResult> AdminLogin([FromBody] AdminLoginModel model)
        {
            try
            {
                if (model.Username == "joonadmin" && model.Password == "joonadmin@123")
                {
                    var tokenHandler = new JwtSecurityTokenHandler();
                    var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]);
                    var tokenDescriptor = new SecurityTokenDescriptor
                    {
                        Subject = new ClaimsIdentity(new[]
                        {
                            new Claim(ClaimTypes.NameIdentifier, model.Username),
                            new Claim(ClaimTypes.Role, "Admin")
                        }),
                        Expires = DateTime.UtcNow.AddHours(24),
                        Issuer = _configuration["Jwt:Issuer"],
                        Audience = _configuration["Jwt:Audience"],
                        SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
                    };

                    var token = tokenHandler.CreateToken(tokenDescriptor);
                    var tokenString = tokenHandler.WriteToken(token);

                    var result = new
                    {
                        success = true,
                        message = "Admin login successful",
                        token = tokenString
                    };

                    return Ok(result);
                }
                else
                {
                    return Ok(new { success = false, message = "Invalid username or password" });
                }
            }
            catch (Exception e)
            {
                return Ok(new { statusCode = "500", status = "Internal Server Error", message = e.Message });
            }
        }

    }
}