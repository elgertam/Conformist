using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BlogApi.Models;

namespace BlogApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PostsController : ControllerBase
{
    private readonly BlogContext _context;

    public PostsController(BlogContext context)
    {
        _context = context;
    }

    // GET: api/posts
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Post>>> GetPosts()
    {
        return await _context.Posts
            .Include(p => p.Author)
            .Include(p => p.Comments)
            .ToListAsync();
    }

    // GET: api/posts/5
    [HttpGet("{id}")]
    public async Task<ActionResult<Post>> GetPost(int id)
    {
        var post = await _context.Posts
            .Include(p => p.Author)
            .Include(p => p.Comments)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (post == null)
        {
            return NotFound();
        }

        return post;
    }

    // POST: api/posts
    [HttpPost]
    public async Task<ActionResult<Post>> CreatePost(CreatePostRequest request)
    {
        var post = new Post
        {
            Title = request.Title,
            Content = request.Content,
            AuthorId = request.AuthorId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Posts.Add(post);
        
        // Add audit log (this will cause side effects for GET tests - demonstrating RFC violations)
        _context.AuditLogs.Add(new AuditLog
        {
            Action = "CREATE",
            EntityType = "Post",
            EntityId = post.Id,
            UserId = request.AuthorId.ToString()
        });
        
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetPost), new { id = post.Id }, post);
    }

    // PUT: api/posts/5
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdatePost(int id, UpdatePostRequest request)
    {
        var post = await _context.Posts.FindAsync(id);
        if (post == null)
        {
            return NotFound();
        }

        post.Title = request.Title;
        post.Content = request.Content;
        post.UpdatedAt = DateTime.UtcNow;

        // Add audit log
        _context.AuditLogs.Add(new AuditLog
        {
            Action = "UPDATE",
            EntityType = "Post",
            EntityId = id,
            UserId = "system"
        });

        await _context.SaveChangesAsync();

        return NoContent();
    }

    // DELETE: api/posts/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePost(int id)
    {
        var post = await _context.Posts.FindAsync(id);
        if (post == null)
        {
            return NotFound();
        }

        // Add audit log before deletion
        _context.AuditLogs.Add(new AuditLog
        {
            Action = "DELETE",
            EntityType = "Post",
            EntityId = id,
            UserId = "system"
        });

        _context.Posts.Remove(post);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    // HEAD: api/posts (should return same headers as GET without body)
    [HttpHead]
    public async Task<ActionResult> HeadPosts()
    {
        var count = await _context.Posts.CountAsync();
        Response.Headers.Add("X-Total-Count", count.ToString());
        return Ok();
    }

    // OPTIONS: api/posts (should return allowed methods)
    [HttpOptions]
    public IActionResult OptionsPosts()
    {
        Response.Headers.Add("Allow", "GET, POST, HEAD, OPTIONS");
        return Ok();
    }
}

public record CreatePostRequest(string Title, string Content, int AuthorId);
public record UpdatePostRequest(string Title, string Content);