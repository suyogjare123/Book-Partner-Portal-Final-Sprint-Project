﻿using Book_Portal_API.Helpers;
using Book_Portal_API.Models;
using Book_Portal_API.Payloads;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Book_Portal_API.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly PubsContext _context;
        public AuthController(PubsContext context)
        {
            this._context = context;
        }

        [HttpPost("login")]
        public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
        {
            if (request == null)
            {
                return BadRequest();
            }

            ApplicationUser? user = null;
            
            if(request.Role == "author")
            {
                user = await _context.Authors.FirstOrDefaultAsync(a => a.Username == request.Username);
            }
            else if (request.Role == "publisher")
            {
                user = await _context.Publishers.FirstOrDefaultAsync(p => p.Username == request.Username);
            }

            if (user == null)
            {
                return NotFound(new { Message = "User Not Found" });
            }

            if (!PasswordHelper.Decode(request.Password, user.Password))
            {
                await Console.Out.WriteLineAsync(request.Password + " ---------------- " + user.Password);
                return BadRequest(new { Message = "Password Is Incorrect" });
            }

            LoginResponse response = new LoginResponse();

            response.Token = CreateJWT(user);
            response.Message = "Logged In Successfully";

            return Ok(response);
        }

        [HttpPost("author/register")]
        public async Task<ActionResult<RegisterResponse>> RegisterAuthor([FromBody] AuthorRegisterationRequest request)
        {
            if (request == null)
            {
                return BadRequest();
            }
            if (await CheckUserNameExistsAsync(request.Username))
            {
                return BadRequest(new { Message = "Username Already Exists" });
            }
            if (await CheckEmailExistsAsync(request.Email))
            {
                return BadRequest(new { Message = "Email Already Exists" });
            }

            Author author = new Author()
            {
                AuId = GenerateAuthorId(),
                Username = request.Username,
                Password = PasswordHelper.Encode(request.Password),
                Email = request.Email,
                Role = "author",
                AuFname = request.AuFname,
                AuLname = request.AuLname,
                Phone = request.Phone,
                Address = request.Address,
                City = request.City,
                State = request.State,
                Zip = request.Zip,
                Contract = true
            };

            await _context.AddAsync(author);
            await _context.SaveChangesAsync();

            return Ok(new RegisterResponse() { Message = "User Registered Successfully"});
        }


        [HttpPost("publisher/register")]
        public async Task<ActionResult<RegisterResponse>> RegisterPublisher([FromForm] PublisherRegisterRequest request)
        {
            if (request == null)
            {
                return BadRequest();
            }
            if (await CheckUserNameExistsAsync(request.Username))
            {
                await Console.Out.WriteLineAsync(request.Username);
                return BadRequest(new { Message = "Username Already Exists" }) ;
            }
            if (await CheckEmailExistsAsync(request.Email))
            {
                return BadRequest(new { Message = "Email Already Exists" });
            }

            string id = GeneratePublisherId();


            await using var memoryStream = new MemoryStream();
            await request.Logo.CopyToAsync(memoryStream);
            byte[] arr = memoryStream.ToArray();


            Publisher publisher = new Publisher()
            {
                Username = request.Username,
                Password = PasswordHelper.Encode(request.Password),
                Email = request.Email,
                Role = "publisher",
                PubId = id,
                PubName = request.PubName,
                City = request.City,
                State = request.State,
                Country = request.Country,
                PubInfo = new PubInfo()
                {
                    PubId = id,
                    Logo = arr,
                    PrInfo = request.PrInfo
                }

            };

            await _context.Publishers.AddAsync(publisher);
            await _context.SaveChangesAsync();

            return Ok(new RegisterResponse() { Message = "User Registered Successfully"});
        }


        private async Task<bool> CheckUserNameExistsAsync(string username)
        {
            bool res1 = await _context.Authors.AnyAsync(u => u.Username == username);

            bool res2 = await _context.Publishers.AnyAsync(u => u.Username == username);
            return res1 || res2;
        }

        private async Task<bool> CheckEmailExistsAsync(string email)
        {
            bool res1 = await _context.Authors.AnyAsync(u => u.Email == email);

            bool res2 = await _context.Publishers.AnyAsync(u => u.Email == email);

            return res1 || res2;
        }

        private string CreateJWT(ApplicationUser user)
        {
            var jwtTokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes("this is my secret key new");

            var idenity = new ClaimsIdentity(new Claim[]
            {
                new Claim(ClaimTypes.Role,user.Role),
                new Claim(ClaimTypes.Email,user.Email),
                new Claim(JsonClaimValueTypes.Json,JsonConvert.SerializeObject(user))
            });

            var credentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256);

            var tokenDiscriptor = new SecurityTokenDescriptor()
            {
                Subject = idenity,
                Expires = DateTime.Now.AddDays(1),
                SigningCredentials = credentials
            };

            var token = jwtTokenHandler.CreateToken(tokenDiscriptor);

            return jwtTokenHandler.WriteToken(token);
        }

        private string GenerateAuthorId()
        {
            Random rd = new Random();
            string id = rd.Next(100, 999).ToString() + "-" + rd.Next(100, 999).ToString() + "-" + rd.Next(100, 999).ToString();
            return id;
        }

        private string GeneratePublisherId()
        {
            Random rd = new Random();
            string id = rd.Next(1000, 9999).ToString();
            return id;
        }
    }
}
