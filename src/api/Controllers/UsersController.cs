using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using API.Interfaces;
using API.DTOs;
using AutoMapper;
using System.Threading.Tasks;
using System.Collections.Generic;
using API.Extensions;
using Microsoft.AspNetCore.Http;
using API.Entities;
using System.Linq;

namespace API.Controllers
{
    [Authorize]
    public class UsersController : BaseApiController
    {
        private readonly IUserRepository repository;
        private readonly IMapper mapper;
        private readonly IPhotoService photoService;
        public UsersController(IUserRepository repository, IMapper mapper, IPhotoService photoService)
        {
            this.photoService = photoService;
            this.mapper = mapper;
            this.repository = repository;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<MemberDTO>>> GetUsers()
        {
            return Ok(await repository.GetMembersAsync());
        }

        // api/users/3
        // [HttpGet("{id}")]
        // public async Task<ActionResult<MemberDTO>> GetUser(int id)
        // {
        //     return await repository.GetMemberByIdAsync(id);
        // }

        // api/users/John
        [HttpGet("{username}")]
        public async Task<ActionResult<MemberDTO>> GetUser(string username)
        {
            return await repository.GetMemberByUsernameAsync(username);
        }

        [HttpPut]
        public async Task<ActionResult> UpdateUser(MemberUpdateDTO memberUpdateDTO)
        {
            var user = await repository.GetUserByUsernameAsync(User.GetUsername());

            mapper.Map(memberUpdateDTO, user);

            repository.Update(user);

            if (await repository.SaveAllAsync())
            {
                return NoContent();
            }
            return BadRequest("Failed to update user");
        }

        [HttpPost("add-photo")]
        public async Task<ActionResult<PhotoDTO>> AddPhoto(IFormFile file)
        {
            var user = await repository.GetUserByUsernameAsync(User.GetUsername());

            var result = await photoService.AddPhotoAsync(file);

            if (result.Error != null)
            {
                return BadRequest(result.Error);
            }

            var photo = new Photo
            {
                Url = result.SecureUrl.AbsoluteUri,
                PublicId = result.PublicId
            };

            if (user.Photos.Count == 0)
            {
                photo.IsMain = true;
            }

            user.Photos.Add(photo);

            if (await repository.SaveAllAsync())
            {
                return CreatedAtAction(nameof(GetUser),
                    new { username = user.UserName }, mapper.Map<PhotoDTO>(photo));
            }

            return BadRequest("Problem adding photo");
        }

        [HttpPut("set-main-photo/{photoId}")]
        public async Task<ActionResult> SetMainPhoto(int photoId)
        {
            var user = await repository.GetUserByUsernameAsync(User.GetUsername());
            if (user == null) return BadRequest("User doesn't exist");

            var photo = user.Photos.Find(x => x.Id == photoId);

            if (photo == null) return BadRequest("Photo doesn't exist");

            if (photo.IsMain) return BadRequest("This is already your main photo");

            var currentMainPhoto = user.Photos.Find(x => x.IsMain);
            if (currentMainPhoto == null) currentMainPhoto.IsMain = false;

            photo.IsMain = true;

            if (await repository.SaveAllAsync()) return NoContent();

            return BadRequest("Failed to set your main photo");
        }
    }
}