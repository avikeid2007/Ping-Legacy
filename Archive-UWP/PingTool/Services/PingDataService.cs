using PingTool.Helpers;
using PingTool.Models;
using System;
using System.Collections.Generic;

namespace PingTool.Services
{
    public class PingDataService
    {

        public IEnumerable<PingMassage> GetLastHistory()
        {
            return SQLiteHelper.GetAllDistinct();
        }

        public PingMassage GetMassage(int Id)
        {
            return SQLiteHelper.Get(Id);
        }
        public IEnumerable<PingMassage> GetMany(Guid Id)
        {
            return SQLiteHelper.GetAll(Id);
        }
        public void SaveMassage(PingMassage post)
        {
            SQLiteHelper.Save(post);
        }

    }
}
