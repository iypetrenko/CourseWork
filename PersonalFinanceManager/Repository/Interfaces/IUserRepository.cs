﻿using PersonalFinanceManager.Model;

namespace PersonalFinanceManager.Repository.Interface
{
    public interface IUserRepository
    {
        User CheckLogin(string username, string password);
        bool RegisterUser(string username, string password);
        User CreateGuestUser();
        List<User> GetAllUsers();
        bool UpdateUser(User user);
        bool AdminExists();
        void CreateAdminIfNotExists();
    }
}