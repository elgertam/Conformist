using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BlogApi.Models;

namespace BlogApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly BlogContext _context;

    public UsersController(BlogContext context)
    {
        _context = context;
    }

    // GET: api/users
    [HttpGet]
    public async Task<ActionResult<IEnumerable<User>>> GetUsers()
    {
        // This GET method has a side effect - adding to audit log
        // This violates HTTP RFC 7231 Section 4.2.1 (GET should be safe)
        _context.AuditLogs.Add(new AuditLog
        {
            Action = "LIST_USERS",
            EntityType = "User",
            EntityId = 0,
            UserId = "system"
        });
        await _context.SaveChangesAsync();

        return await _context.Users
            .Include(u => u.Posts)
            .ToListAsync();
    }

    // GET: api/users/5
    [HttpGet("{id}")]
    public async Task<ActionResult<User>> GetUser(int id)
    {
        var user = await _context.Users
            .Include(u => u.Posts)
            .Include(u => u.Comments)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
        {
            return NotFound();
        }

        return user;
    }

    // POST: api/users
    [HttpPost]
    public async Task<ActionResult<User>> CreateUser(CreateUserRequest request)
    {
        var user = new User
        {
            Name = request.Name,
            Email = request.Email,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user);
    }

    // PUT: api/users/5
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(int id, UpdateUserRequest request)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        user.Name = request.Name;
        user.Email = request.Email;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    // DELETE: api/users/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    // HEAD: api/users (should match GET headers)
    [HttpHead]
    public async Task<ActionResult> HeadUsers()
    {
        var count = await _context.Users.CountAsync();
        Response.Headers.Add("X-Total-Count", count.ToString());
        return Ok();
    }

    // OPTIONS: api/users (missing Allow header - RFC violation)
    [HttpOptions]
    public IActionResult OptionsUsers()
    {
        // This violates RFC 7231 Section 4.3.7 - missing Allow header
        return Ok("Options response without Allow header");
    }
}

public record CreateUserRequest(string Name, string Email);
public record UpdateUserRequest(string Name, string Email);