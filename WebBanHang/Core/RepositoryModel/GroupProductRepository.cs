using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data.Entity;
using System.Linq;
using WebBanLinhKienDienTu.Models;
using WebBanLinhKienDienTu.Utils;

namespace WebBanLinhKienDienTu.Core.RepositoryModel
{
    public class GroupProductRepository : RepositoryModel<GroupProduct>
    {
        public GroupProductRepository(DbContext db) : base(db)
        {
        }

        public IEnumerable<GroupProduct> GetTopGroupProducts()
        {
            return FetchAll().Where(item => item.ParentGroupID == null).OrderBy(item => item.GroupName).ToList();
        }

        public List<Product> GetProductInGroups(int group, List<Product> products = null)
        {
            if (products == null) products = new List<Product>();
            var currGroup = FindById(group);
            products.AddRange(currGroup.Products.Where(item => item.Active == true));
            //Find subcategory
            List<GroupProduct> subGroups = FetchAll().Where(item => item.ParentGroupID == group).ToList();
            foreach (GroupProduct subGroup in subGroups)
            {
                GetProductInGroups(subGroup.GroupID, products);
            }
            return products;
        }

        public List<Product> GetProductInGroups(int group, NameValueCollection filter)
        {
            // Không áp dụng sort mặc định ngay từ đầu để tránh conflict với sort được chọn
            IEnumerable<Product> model = GetProductInGroups(group);

            if (!String.IsNullOrEmpty(filter["range_price"]))
            {
                string[] minMax = filter["range_price"].Split(',');
                if (minMax.Length == 2)
                {
                    long min = minMax[0].ToIntWithDef(0);
                    long max = minMax[1].ToIntWithDef(0);
                    // Lọc theo giá: ưu tiên SalePrice nếu có, nếu không thì dùng Price
                    model = model.Where(item => {
                        long itemPrice = item.isSale() && item.SalePrice > 0 ? item.SalePrice : item.Price;
                        return itemPrice >= min && itemPrice <= max;
                    });
                }
            }

            // Áp dụng sort: nếu có sort thì dùng sort đó, nếu không thì dùng sort mặc định
            if (!String.IsNullOrEmpty(filter["sort"]) && filter["sort"] != "default")
            {
                switch (filter["sort"])
                {
                    case "name_asc":
                        model = model.OrderBy(item => item.ProductName);
                        break;

                    case "name_desc":
                        model = model.OrderByDescending(item => item.ProductName);
                        break;

                    case "price_asc":
                        model = model.OrderBy(item => item.isSale() && item.SalePrice > 0 ? item.SalePrice : item.Price);
                        break;

                    case "price_desc":
                        model = model.OrderByDescending(item => item.isSale() && item.SalePrice > 0 ? item.SalePrice : item.Price);
                        break;

                    case "newest":
                        model = model.OrderByDescending(item => item.CreateDate);
                        break;

                    case "oldest":
                        model = model.OrderBy(item => item.CreateDate);
                        break;

                    case "discount_desc":
                        model = model.OrderByDescending(item => item.isSale() ? 
                            ((item.Price - item.SalePrice) * 100.0 / item.Price) : 0);
                        break;

                    case "rating_desc":
                        model = model.OrderByDescending(item => item.Comments.Any() ? 
                            item.Comments.Average(c => (double?)c.Rate) : 0);
                        break;
                }
            }
            else
            {
                // Sort mặc định: mới nhất trước
                model = model.OrderByDescending(item => item.CreateDate);
            }
            
            return model.ToList();
        }

        public IEnumerable<GroupProduct> GetListSubGroups(int groupID)
        {
            var mainGroupID = GetMainGroup(groupID);
            var subGroups = FetchAll()
                .Where(item => item.ParentGroupID == mainGroupID)
                .OrderByDescending(item => item.Priority)
                .ToList();

            // Nếu category hiện tại không có sub-group nào, hiển thị các nhóm cùng cấp
            if (!subGroups.Any())
            {
                var currentGroup = FindById(groupID);
                if (currentGroup != null)
                {
                    if (currentGroup.ParentGroupID.HasValue)
                    {
                        var parentId = currentGroup.ParentGroupID.Value;
                        subGroups = FetchAll()
                            .Where(item => item.ParentGroupID == parentId)
                            .OrderByDescending(item => item.Priority)
                            .ToList();
                    }
                    else
                    {
                        // Category top-level không có con -> fallback toàn bộ nhóm top-level
                        subGroups = FetchAll()
                            .Where(item => item.ParentGroupID == null)
                            .OrderByDescending(item => item.Priority)
                            .ToList();
                    }
                }
            }

            return subGroups;
        }

        public int GetMainGroup(int groupID)
        {
            var currGroup = FindById(groupID);
            if (currGroup.ParentGroupID == null)
                return groupID;
            return GetMainGroup(currGroup.ParentGroupID.GetValueOrDefault(0));
        }
    }
}