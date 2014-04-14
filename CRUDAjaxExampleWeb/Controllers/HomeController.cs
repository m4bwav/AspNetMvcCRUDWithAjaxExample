using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Data;
using CRUDAjaxExampleWeb.Models;

namespace CRUDAjaxExampleWeb.Controllers
{
    public class HomeController : Controller
    {
        private DeveloperTestDbContext db = new DeveloperTestDbContext();

        public ActionResult Index()
        {
            return View(db.People.ToList());
        }

        public ViewResult Details(Guid id)
        {
            Person person = db.People.Find(id);
            return View(person);
        }

        public ActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public ActionResult Create(Person person)
        {
            if (ModelState.IsValid)
            {
                person.Id = Guid.NewGuid();
                db.People.Add(person);
                db.SaveChanges();
                return RedirectToAction("Index");
            }

            return View(person);
        }

        public ActionResult Edit(Guid id)
        {
            var person = db.People.Find(id);

            return View(person);
        }


        [HttpPost]
        public ActionResult CalculateAge(Guid id)
        {
            var person = db.People.Find(id);

            return Json(new { person.Age });
        }

        [HttpPost]
        public ActionResult Edit(Person person)
        {
            if (ModelState.IsValid)
            {
                db.Entry(person).State = EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            return View(person);
        }

        protected override void Dispose(bool disposing)
        {
            db.Dispose();

            base.Dispose(disposing);
        }

    }
}
