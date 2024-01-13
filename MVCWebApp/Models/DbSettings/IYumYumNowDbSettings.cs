﻿namespace MVCWebApp.Models.DbSettings;

public interface IYumYumNowDbSettings
{
    string UsersCollectionName { get; set; }
    string ProductsCollectionName { get; set; }
    string ConnectionString { get; set; }
    string DatabaseName { get; set; }
}
