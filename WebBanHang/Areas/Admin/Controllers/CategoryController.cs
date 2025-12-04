using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Web.Mvc;
using WebBanLinhKienDienTu.Core;
using WebBanLinhKienDienTu.Models;
using WebBanLinhKienDienTu.ViewModels;

namespace WebBanLinhKienDienTu.Areas.Admin.Controllers
{
    [Security]
    public class CategoryController : AdminBaseController
    {
        //
        // GET: /Admin/Category/
        public ActionResult Index()
        {
            var groups = Repository.GroupProduct.FetchAll().Where(g => g.ParentGroupID == null);
            ViewBag.Groups = groups;
            return View();
        }

        public ActionResult Create()
        {
            var model = new AdminGroupProductViewModel();
            ViewBag.Groups = Repository.GroupProduct.FetchAll().Where(g => g.ParentGroupID == null);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(AdminGroupProductViewModel model)
        {
            if (ModelState.IsValid)
            {
                var group = Mapper.Map<GroupProduct>(model);
                Repository.GroupProduct.Insert(group);
                Repository.GroupProduct.SaveChanges();
                if (group.GroupID != 0)
                {
                    return RedirectToAction("Index", "Category");
                }
            }
            ViewBag.Groups = Repository.GroupProduct.FetchAll().Where(g => g.ParentGroupID == null);
            return View(model);
        }

        public ActionResult Edit(int? id)
        {
            if (id == 0)
            {
                return HttpNotFound();
            }
            var group = Repository.GroupProduct.FindById(id);
            if (group == null) return HttpNotFound();

            var model = Mapper.Map<AdminGroupProductViewModel>(group);
            // Map ParentGroupID manually since it's Nullable<int> to string
            model.ParentGroupID = group.ParentGroupID.HasValue ? group.ParentGroupID.Value.ToString() : null;
            ViewBag.Groups = Repository.GroupProduct.FetchAll().Where(g => g.ParentGroupID == null);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(AdminGroupProductViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Groups = Repository.GroupProduct.FetchAll().Where(g => g.ParentGroupID == null);
                return View(model);
            }

            try
            {
                var group = Repository.GroupProduct.FindById(model.GroupID);
                if (group == null)
                {
                    return HttpNotFound();
                }
                
                // Update properties
                group.GroupName = model.GroupName;
                
                // Handle ParentGroupID conversion
                if (string.IsNullOrEmpty(model.ParentGroupID) || model.ParentGroupID == "/")
                {
                    group.ParentGroupID = null;
                }
                else
                {
                    int parentId;
                    if (int.TryParse(model.ParentGroupID, out parentId))
                    {
                        group.ParentGroupID = parentId;
                    }
                    else
                    {
                        group.ParentGroupID = null;
                    }
                }
                
                group.Icon = model.Icon;
                group.Priority = model.Priority;
                
                // Save changes
                Repository.GroupProduct.SaveChanges();
                
                // Redirect based on button clicked
                if (Request.Form["save-continue"] != null)
                    return RedirectToAction("Edit", "Category", new { id = group.GroupID });
                
                return RedirectToAction("Index", "Category");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Có lỗi xảy ra khi lưu: " + ex.Message);
                ViewBag.Groups = Repository.GroupProduct.FetchAll().Where(g => g.ParentGroupID == null);
                return View(model);
            }
        }

        public ActionResult LoadCategory(int start, int length)
        {
            var search = Request.QueryString["search[value]"].ToString();
            var parentGroup = Request.QueryString["columns[2][search][value]"].ToString();
            var cates = Repository.GroupProduct.FetchAll().Where(c => c.GroupName.Contains(search));
            if (!String.IsNullOrEmpty(parentGroup) && !parentGroup.Equals("all"))
            {
                int? groupID = (int.Parse(parentGroup) as int?);
                cates = cates.Where(c => c.ParentGroupID == groupID);
            }
            cates = cates.OrderByDescending(c => c.Priority);
            var catePaging = cates.Skip(start).Take(length);
            List<object> data = new List<object>();
            foreach (var cate in catePaging)
            {
                var row = new List<object>();
                row.Add(cate.GroupID.ToString());
                row.Add(cate.GroupName);
                row.Add(cate.ParentGroupID);
                row.Add(cate.Icon);
                row.Add(cate.Priority);
                data.Add(row);
            }
            var result = new
            {
                draw = Request.QueryString["draw"],
                recordsTotal = cates.Count(),
                recordsFiltered = cates.Count(),
                search = search,
                data = data
            };
            return Content(JsonConvert.SerializeObject(result), "application/json");
        }

        public ActionResult Delete(int? id, bool confirm = false)
        {
            dynamic result = new ExpandoObject();
            if (id == null)
            {
                result.status = "error";
                result.title = "L?i";
                result.message = "Kh�ng c� m� id nh�m";
                return Content(JsonConvert.SerializeObject(result), "application/json");
            }

            var group = Repository.GroupProduct.FindById(id);
            if (group == null)
            {
                result.status = "error";
                result.title = "L?i";
                result.message = "Nh�m n�y kh�ng t?n t?i trong h? th?ng";
                return Content(JsonConvert.SerializeObject(result), "application/json");
            }
            if (group.Products.Count > 0 && !confirm)
            {
                result.status = "warning";
                result.title = "C?nh b�o";
                result.message = "Nh�m n�y ch?a nhi?u s?n ph?m, khi x�a s? m?t h?t s?n ph?m, h�y c�n nh?c tru?c khi x�a";
                return Content(JsonConvert.SerializeObject(result), "application/json");
            }
            Repository.GroupProduct.Delete(id);
            Repository.SaveChanges();
            if (Repository.GroupProduct.FetchAll().Any(g => g.GroupID == id))
            {
                result.status = "error";
                result.title = "L?i";
                result.message = "�� c� l?i x?y ra, kh�ng th? x�a du?c";
                return Content(JsonConvert.SerializeObject(result), "application/json");
            }
            result.status = "success";
            result.title = "Th�nh c�ng";
            result.message = "Ch�c m?ng b?n d� x�a th�nh c�ng";
            return Content(JsonConvert.SerializeObject(result), "application/json");
        }
    }
}