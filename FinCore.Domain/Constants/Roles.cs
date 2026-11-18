using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Domain.Constants
{
    public static class Roles
    {
        public const string Admin = "Admin";
        public const string Manager = "Manager";
        public const string Customer = "Customer";

        
        public const string ManagerOrAdmin = Manager + "," + Admin;

        public static readonly string[] All = { Admin, Manager, Customer };
    }
}
