﻿using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using MVCWebApp.Models;
using MVCWebApp.Models.JWTSettings;
using MVCWebApp.Models.UserDB;
using MVCWebApp.Services.EncryptorService;
using MVCWebApp.Services.HasherService;
using MVCWebApp.Services.UserService;

namespace MVCWebApp;

public class AuthController : Controller
{
    private readonly ILogger<AuthController> _logger;
    private readonly IPasswordHasher _hasher;
    private readonly IAesEncryptor _encryptor;
    private readonly IUserService _userService;
    private readonly IJwtSettings _JwtSettings;

    public AuthController(ILogger<AuthController> logger, IUserService userService, 
        IPasswordHasher hasher, IAesEncryptor encryptor, IJwtSettings JwtSettings)
    {
        _logger = logger;
        _hasher = hasher;
        _encryptor = encryptor;
        _userService = userService;
        _JwtSettings = JwtSettings;
    }

    public IActionResult Reset() => View();

    [HttpGet]
    public IActionResult ForgotPassword() => View();

    // Displays the login form.
    [HttpGet]
    public IActionResult Login() => View();

    // Handles the login form submission.
    [HttpPost]
    public async Task<IActionResult> LoginAsync([FromBody] LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            _logger.LogInformation("Invalid model.");
            return View(model);
        }

        _logger.LogInformation($"Email: {model.Email}, Passsword: {model.Password}");

        var user = await _userService.GetByEmail(_encryptor.EncryptString(model.Email));

        if (user is null)
        {
            _logger.LogInformation("Invalid login attempt.");
            return View(model);
        }

        if (!_hasher.VerifyString(user.PasswordHash, model.Password))
        {
            _logger.LogInformation("Invalid password.");
            return View(model);
        }
        
        var jwt = CreateToken(user);

        // Log successful login
        _logger.LogInformation($"User logged in: {model.Email}");

        // Redirect to the main page
        return RedirectToAction("Index", "Home", new { Token = jwt});
    }

    // Displays the registration form.
    [HttpGet]
    public IActionResult Register() => View();

    // Handles the registration form submission.
    [HttpPost]
    public async Task<IActionResult> RegisterAsync(RegisterViewModel model)
    {
        if (!ModelState.IsValid) 
        {
            _logger.LogInformation("Invalid model.");
            return View(model);
        }

        var email = _encryptor.EncryptString(model.Email);
        var phone = _encryptor.EncryptString(model.PhoneNumber);

        // Check if a user with the same email or phone already exists
        if (await _userService.GetByEmail(email) != null || 
            await _userService.GetByPhone(phone) != null)
        {
            _logger.LogInformation("User with this email and/or phone is already registered.");
            return View(model);
        }
        
        // Creating a new instance of user
        var user = new User
        {
            FirstName = model.FirstName,
            LastName = model.LastName,
            Email = email,
            PasswordHash = _hasher.HashString(model.Password),
            PhoneNumber = phone        
        };

        // Check if user creation was successful
        if (user is null)
        {
            _logger.LogError("Error with creating user.");
            return View(model);
        }

        // Save the user to the database
        await _userService.Create(user);

        // Log successful registration
        _logger.LogInformation($"User registered: {user.Id}");

        var jwt = CreateToken(user);

        SignInWithJwt(jwt);

        // Redirect to the main page
        return RedirectToAction("Index", "Home");        
    }

    private void SignInWithJwt(string jwt)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Expires = DateTimeOffset.UtcNow.AddMinutes(_JwtSettings.TokenValidityMinutes),
        };

        HttpContext.Response.Cookies.Append("YourCustomAuthCookie", jwt, cookieOptions);
    }

    private string CreateToken(User user)
    {
        List<Claim> claims = new()
        {
            new Claim(ClaimTypes.Name, user.FirstName)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_JwtSettings.SigningKey));

        return new JwtSecurityTokenHandler()
        .WriteToken(new JwtSecurityToken(
            claims: claims,
            expires: DateTime.Now.AddMinutes(_JwtSettings.TokenValidityMinutes),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature)
        ));
    }
}
