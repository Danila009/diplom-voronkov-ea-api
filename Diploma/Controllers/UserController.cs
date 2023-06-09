﻿using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Diploma.Auth;
using Diploma.Database;
using Diploma.model.user;
using Diploma.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Diploma.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UserController : ControllerBase
{
    private EfModel _efModel;
    private FileRepository imageRepository = new FileRepository();

    public UserController(EfModel model)
    {
        _efModel = model;
    }

    [Authorize]
    [HttpPut]
    public async Task<ActionResult<User>> UpdateUser(UpdateUserDto dto)
    {
        if (HttpContext.User.Identity is not ClaimsIdentity identity)
            return NotFound();

        var id = Convert.ToInt32(identity.FindFirst("Id")?.Value);

        var user = await _efModel.Users.FindAsync(id);

        if (user == null)
            return NotFound();

        user.Login = dto.Login;
        user.FirstName = dto.FirstName;
        user.LastName = dto.LastName;
        user.MidleName = dto.MidleName;
        user.Police = dto.Police;

        _efModel.Entry(user).State = EntityState.Modified;
        await _efModel.SaveChangesAsync();

        return user;
    }

    //[Authorize(Roles = "AdminUser")]
    [HttpGet("/api/Users")]
    public async Task<ActionResult<List<UserDto>>> GetUsers(string? search, string? role)
    {
        IQueryable<User> users = _efModel.Users;

        if(search != null)
        {
            users = users.Where(u =>
                u.FirstName.Contains(search)
                || u.MidleName.Contains(search) || u.LastName.Contains(search));
        }

        if(role != null)
        {
            users = users.Where(u => u.Role == role);
        }

        var usersList = await users.ToListAsync();
        List<UserDto> usersDto = new();

        foreach (var user in usersList)
        {
            Doctor? doctor = null;
            Admin? admin = null;

            if (user.Role == "DoctorUser")
            {
                doctor = await _efModel.Doctors
                    .Include(u => u.Post)
                    .FirstOrDefaultAsync(u => u.Id == user.Id);
            }else if(user.Role == "AdminUser")
            {
                admin = await _efModel.Admins.FindAsync(user.Id);
            }

            var dto = new UserDto
            {
                Id = user.Id,
                Login = user.Login,
                FirstName = user.FirstName,
                LastName = user.LastName,
                MidleName = user.MidleName,
                Police = user.Police,
                Photo = user.Photo,
                Role = user.Role,
                Admin = admin,
                Doctor = doctor
            };

            usersDto.Add(dto);
        }

        return usersDto;
    }

    [Authorize]
    [HttpGet]
    public async Task<ActionResult<User>> Get()
    {
        if (HttpContext.User.Identity is not ClaimsIdentity identity)
            return NotFound();
            
        var id = Convert.ToInt32(identity.FindFirst("Id")?.Value);

        var user = await _efModel.Users.FirstOrDefaultAsync(u => u.Id == id);

        return Ok(user);
    }

    [Authorize]
    [HttpPatch("Photo")]
    public async Task<ActionResult> UpdatePhoto(IFormFile file)
    {
        if (HttpContext.User.Identity is not ClaimsIdentity identity)
            return NotFound();

        var id = Convert.ToInt32(identity.FindFirst("Id")?.Value);

        var user = await _efModel.Users.FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
            return NoContent();

        var uri = await imageRepository.UploadFile(
            path: $"resources/users/{id}/photo/",
            fileId: id.ToString(),
            file: file
        );

        var url = $"http://localhost:5000/api/User/{id}/Photo.jpg?uri={uri}";

        user.Photo = url;

        _efModel.Entry(user).State = EntityState.Modified;

        await _efModel.SaveChangesAsync();

        return Ok(url);
    }

    [HttpGet("{id}/Photo.jpg")]
    public ActionResult GetUserPhoto(int id, string uri)
    {
        byte[]? file = imageRepository.GetFile(
            uri
        );

        if (file != null)
            return File(file, "image/jpeg");
        else
            return NotFound();
    }

    [HttpPost("/api/Registration")]
    public async Task<ActionResult> PostRegistration(RegistrationDTO userDTO)
    {
        if (userDTO == null)
            return BadRequest();

        _efModel.Users.Add(new User
        {
            Password = userDTO.Password,
            Login = userDTO.Login,
            Police = userDTO.Police,
            FirstName = userDTO.FirstName,
            LastName = userDTO.LastName,
            MidleName = userDTO.MidleName
        });

        await _efModel.SaveChangesAsync();

        return Ok();
    }

    [Authorize(Roles = "AdminUser")]
    [HttpPost("/api/Registration/Doctor")]
    public async Task<ActionResult> PostRegistrationDoctor(DoctorRegistrationDTO userDTO)
    {
        var user = await _efModel.Users.FindAsync(userDTO.UserId);

        if (user == null)
            return BadRequest();

        var post = await _efModel.PostDoctors.FindAsync(userDTO.PostId);

        if (post == null)
            return BadRequest();

        _efModel.Users.Remove(user);
        await _efModel.Doctors.AddAsync(new Doctor
        {
            Id = user.Id,
            Password = user.Password,
            Offece = userDTO.Offece,
            Login = user.Login,
            Police = user.Police,
            FirstName = user.FirstName,
            LastName = user.LastName,
            MidleName = user.MidleName,
            Post = post
        });

        await _efModel.SaveChangesAsync();

        return Ok();
    }

    [HttpPost("/api/Authorization")]
    public ActionResult<object> Token(AuthorizationDTO authorization)
    {
        var indentity = GetIdentity(authorization.Login, authorization.Password);

        if (indentity == null)
        {
            return BadRequest();
        }

        var now = DateTime.UtcNow;
        var jwt = new JwtSecurityToken(
            audience: TokenBaseOptions.AUDIENCE,
            issuer: TokenBaseOptions.ISSUER,
            notBefore: now,
            claims: indentity.Claims,
            expires: now.Add(TimeSpan.FromDays(TokenBaseOptions.LIFETIME)),
            signingCredentials: new SigningCredentials(TokenBaseOptions.GetSymmetricSecurityKey(),
                SecurityAlgorithms.HmacSha256));

        var encodedJwt = new JwtSecurityTokenHandler().WriteToken(jwt);

        var response = new
        {
            access_token = encodedJwt,
            username = indentity.Name,
            role = indentity.FindFirst(ClaimsIdentity.DefaultRoleClaimType).Value
        };

        return response;
    }
    
    [NonAction]
    public ClaimsIdentity? GetIdentity(string login, string password)
    {
        var user = _efModel.Users.FirstOrDefault(
            x => x != null && x.Login == login && x.Password == password);

        if (user != null)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimsIdentity.DefaultNameClaimType, user.FirstName),
                new Claim(ClaimsIdentity.DefaultRoleClaimType, user.Role),
                new Claim("Id", user.Id.ToString())
            };

            ClaimsIdentity claimsIdentity =
                new ClaimsIdentity(claims, "Token", ClaimsIdentity.DefaultNameClaimType,
                    ClaimsIdentity.DefaultRoleClaimType);

            return claimsIdentity;
        }
        
        return null;
    }
}