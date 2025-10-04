using System;
using App.Commons.Paging;
using App.Commons.ResponseModel;
using App.Entities.DTOs.Accounts;

namespace App.BLL.Interfaces;

public interface IAccountService
{
    Task<BaseResponseModel<List<RoleOverviewDTO>>> GetAllUserRoles(long userId);
    Task<BaseResponseModel<UserDetailDTO>> AddRoleToUser(long userId, List<string> roles, long loggedUserId);

    Task<BaseResponseModel<UserDetailDTO>> RemoveRoleFromUserRoles(int userId, List<string> roles);

    Task<BaseResponseModel<PagingDataModel<UserOverviewDTO, GetUsersQueryDTO>>> GetUsers(GetUsersQueryDTO query);

    Task<BaseResponseModel> SoftDeleteUser(int userId);
}