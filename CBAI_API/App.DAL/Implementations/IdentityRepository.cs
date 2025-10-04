using System;
using System.Web;
using App.Commons.Extensions;
using App.Commons.ResponseModel;
using App.DAL.Context;
using App.DAL.Interfaces;
using App.DAL.UnitOfWork;
using App.Entities.DTOs.Accounts;
using App.Entities.Entities.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace App.DAL.Implementations;

public class IdentityRepository : IIdentityRepository
{
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<Role> _roleManager;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;

    public IdentityRepository(IConfiguration config, UserManager<User> userManager,
        RoleManager<Role> roleManager, MyDbContext dbContext,
        IUnitOfWork unitOfWork)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        this._unitOfWork = unitOfWork;
        _configuration = config;
    }


    public Task<User> GetByEmailAsync(string email)
    {
        // Try exact email first, then a dot-insensitive match similar to GetByEmailOrUserNameAsync
        return _userManager.FindByEmailAsync(email);
    }

    public async Task<BaseResponseModel> AddUserAsync(User user, string password, string role)
    {
        try
        {
            await _unitOfWork.BeginTransactionAsync();
            var addUser = await _userManager.CreateAsync(user, password);
            if (!addUser.Succeeded)
                return new BaseResponseModel { IsSuccess = false, Message = "Thêm người dùng thất bại." };

            var existedRole = await _roleManager.RoleExistsAsync(role);
            if (!existedRole)
            {
                await _unitOfWork.RollBackAsync();
                return new BaseResponseModel
                { IsSuccess = false, Message = $"Không tìm thấy vai trò {role} trong hệ thống." };
            }

            var existedUser = await _userManager.FindByNameAsync(user.UserName);
            if (existedUser is null)
            {
                return new BaseResponseModel
                { IsSuccess = false, Message = $"Không tìm thấy người dùng trong hệ thống." };
            }

            var addRole = await _userManager.AddToRoleAsync(existedUser, role);
            if (!addRole.Succeeded)
            {
                await _unitOfWork.RollBackAsync();
                return new BaseResponseModel
                { IsSuccess = false, Message = "Hệ thống thêm vai trò cho người dùng thất bại." };
            }
            await _unitOfWork.CommitTransactionAsync();
            return new BaseResponseModel { IsSuccess = true, Message = "Thêm tài khoản thành công." };
        }
        catch (Exception)
        {
            await _unitOfWork.RollBackAsync();
            throw;
        }
    }

    public async Task<List<User>> GetAccounts(GetUsersQueryDTO dto)
    {
        var users = _userManager.Users
        .AsNoTracking()
        .AsQueryable();

        users = users.Where(x => x.DeletedAt == null);

        if (!string.IsNullOrEmpty(dto.Keyword))
            users = users.Where(x => x.Email.Contains(dto.Keyword) || x.UserName.Contains(dto.Keyword));

        dto.TotalRecord = await users.CountAsync();
        var response = await users.ToPagedList(dto.PageNumber, dto.PageSize).ToListAsync();
        return response;
    }

    /// <summary>
    /// This is used to find a user by Email or UserName
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public async Task<User?> GetByEmailOrUserNameAsync(string input)
    {
        if (input.Contains("@"))
        {
            // Xử lý như email
            var user = await _userManager.FindByEmailAsync(input);
            if (user == null)
            {
                user = await _userManager.Users.FirstOrDefaultAsync(
                    x => x.Email.Replace(".", "") == input.Replace(".", "")
                );
            }

            return user;
        }
        else
        {
            // Xử lý như tên người dùng
            return await _userManager.FindByNameAsync(input);
        }
    }

    public async Task<long> AddUserAsync(User dto, string password)
    {
        IdentityResult result;
        if (string.IsNullOrEmpty(password))
        {
            result = await _userManager.CreateAsync(dto);
        }
        else
        {
            result = await _userManager.CreateAsync(dto, password);
        }

        if (result.Succeeded)
            return dto.Id;
        return -1;
    }

    public async Task<bool> UpdateAsync(User user)
    {
        var result = await _userManager.UpdateAsync(user);
        return result.Succeeded;
    }

    public async Task<User?> GetByIdAsync(long id)
    {
        return await _userManager.FindByIdAsync(id.ToString());
    }

    public Task<User> GetByExternalIdAsync(string id)
    {
        throw new NotImplementedException();
    }

    public async Task<bool> CheckPasswordAsync(User user, string password)
    {
        return await _userManager.CheckPasswordAsync(user, password);
    }

    public Task<bool> HasPasswordAsync(User dto)
    {
        throw new NotImplementedException();
    }

    public Task<IdentityResult> AddPasswordAsync(User dto, string password)
    {
        throw new NotImplementedException();
    }

    public Task<User> GetByPhoneAsync(string phoneNumber)
    {
        throw new NotImplementedException();
    }

    public async Task<bool> ConfirmEmailAsync(string userId, string code)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(code))
            return false;

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return false;

        // Decode URL-encoded token (vì token truyền qua URL có thể bị encode)
        var decodedToken = HttpUtility.UrlDecode(code)?.Replace(" ", "+");

        var result = await _userManager.ConfirmEmailAsync(user, decodedToken);
        return result.Succeeded;
    }

    public async Task<bool> VerifyEmailAsync(User user, string token)
    {
        var decodedToken = HttpUtility.UrlDecode(token);
        var result = await _userManager.ConfirmEmailAsync(user, decodedToken.Replace(" ", "+"));
        if (result.Succeeded) return true;
        return false;
    }

    public async Task<bool> ChangePasswordAsync(User user, string newPassword)
    {
        if (user == null)
        {
            return false;
        }

        var result = await _userManager.RemovePasswordAsync(user);
        if (!result.Succeeded)
        {
            return false;
        }

        result = await _userManager.AddPasswordAsync(user, newPassword);
        return result.Succeeded;
    }

    public async Task<bool> ChangePassword(string userId, string passwordNew)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return false;
        }

        var result = await _userManager.RemovePasswordAsync(user);
        if (!result.Succeeded)
        {
            return false;
        }

        result = await _userManager.AddPasswordAsync(user, passwordNew);
        return result.Succeeded;
    }

    public async Task<bool> CreateUpdateRoleAsync(string roleName, bool isAdmin)
    {
        var roleExists = await _roleManager.RoleExistsAsync(roleName);

        if (!roleExists)
        {
            var newRole = new Role
            {
                Name = roleName,
                IsAdmin = isAdmin
            };
            var result = await _roleManager.CreateAsync(newRole);
            if (result.Succeeded)
            {
                return true;
            }
        }
        else
        {
            var role = await _roleManager.FindByNameAsync(roleName);
            if (role != null)
            {
                role.IsAdmin = isAdmin;
                var result = await _roleManager.UpdateAsync(role);
                if (result.Succeeded)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    ///kKiểm tra role tồn tại
    /// </summary>
    /// <param name="roleName"></param>
    /// <returns></returns>
    public async Task<bool> IsRoleExist(string roleName)
    {
        return await _roleManager.RoleExistsAsync(roleName);
    }

    public async Task<bool> AddRoleByNameAsync(string userId, string roleName)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user != null)
        {
            var role = await _roleManager.FindByNameAsync(roleName);
            if (role != null)
            {
                var result = await _userManager.AddToRoleAsync(user, roleName);
                if (result.Succeeded)
                    return true;
            }
        }

        return false;
    }


    /// <summary>
    /// Adds a list of roles to a user.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="roles"></param>
    /// <returns></returns>
    public async Task<bool> AddRolesToUserAsync(User user, List<string> roles)
    {
        var result = await _userManager.AddToRolesAsync(user, roles);
        return result.Succeeded;
    }


    /// <summary>
    /// Removes a list of roles from a user.
    /// </summary>
    /// <param name="user"></param>
    /// <param name="roles"></param>
    /// <returns></returns>
    public async Task<bool> RemoveRolesFromUserAsync(User user, List<string> roles)
    {
        var result = await _userManager.RemoveFromRolesAsync(user, roles);
        return result.Succeeded;
    }

    public async Task<string> GenerateEmailConfirmationTokenAsync(User user)
    {
        return await _userManager.GenerateEmailConfirmationTokenAsync(user);
    }

    public Task<string> GeneratePasswordResetTokenAsync(User user)
    {
        // Delegate to UserManager to generate a password reset token
        return _userManager.GeneratePasswordResetTokenAsync(user);
    }

    public Task<bool> ResetPasswordAsync(string userId, string token, string newPassword)
    {
        // Decode token (URL-decoded tokens may have spaces instead of '+') and perform reset
        return ResetPasswordInternalAsync(userId, token, newPassword);
    }

    private async Task<bool> ResetPasswordInternalAsync(string userId, string token, string newPassword)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token) || string.IsNullOrEmpty(newPassword))
            return false;

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return false;

        var decodedToken = HttpUtility.UrlDecode(token)?.Replace(" ", "+");
        var result = await _userManager.ResetPasswordAsync(user, decodedToken ?? token, newPassword);
        return result.Succeeded;
    }

    public Task<bool> IsUserInRole(User user, string role)
    {
        throw new NotImplementedException();
    }

    public Task<bool> DeleteRoleByUser(long userId)
    {
        throw new NotImplementedException();
    }

    public Task<IdentityResult> DeleteUser(long userId)
    {
        throw new NotImplementedException();
    }

    public Task<bool> DeleteListRole(long[] ids)
    {
        throw new NotImplementedException();
    }

    public async Task<string[]> GetRolesAsync(long userId)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
                return null;
            var roles = await _userManager.GetRolesAsync(user);
            return roles.ToArray();
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<List<Role>> GetUserRolesAsync(long userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
            return null;

        var roleNames = await _userManager.GetRolesAsync(user);
        if (roleNames == null || roleNames.Count == 0)
            return new List<Role>();

        var roles = await _roleManager.Roles
            .Where(r => roleNames.Contains(r.Name))
            .ToListAsync();

        return roles;
    }

    public Task<bool> VerifyPermission(long userId, string claim)
    {
        throw new NotImplementedException();
    }

    public Task<List<Role>> GetRolesAdmin()
    {
        throw new NotImplementedException();
    }

    public async Task<List<User>> GetUsersInRoleAsync(string roleName)
    {
        var users = await _userManager.GetUsersInRoleAsync(roleName);
        return users?.ToList() ?? new List<User>();
    }
}