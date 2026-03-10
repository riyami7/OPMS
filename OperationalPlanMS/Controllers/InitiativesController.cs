                "Id", "NameAr", model.OrganizationId);

            model.FiscalYears = new SelectList(
                await _db.FiscalYears.OrderByDescending(f => f.Year).ToListAsync(),
                "Id", "NameAr", model.FiscalYearId);

            if (model.OrganizationId > 0)
            {
                model.OrganizationalUnits = new SelectList(
                    await _db.ExternalOrganizationalUnits
                        .Where(u => u.IsActive && u.OrganizationId == model.OrganizationId)
                        .ToListAsync(),
                    "Id", "NameAr", model.OrganizationalUnitId);
            }
            else
            {
                model.OrganizationalUnits = new SelectList(Enumerable.Empty<SelectListItem>());
            }

            model.Supervisors = new SelectList(
                await _db.Users.Where(u => u.IsActive).ToListAsync(),
                "Id", "FullNameAr", model.SupervisorId);
        }

        // API: جلب الوحدات التنظيمية حسب المنظمة
        [HttpGet]
        public async Task<IActionResult> GetUnitsByOrganization(int organizationId)
        {
            var units = await _db.ExternalOrganizationalUnits
                .Where(u => u.OrganizationId == organizationId && u.IsActive)
                .Select(u => new { id = u.Id, nameAr = u.NameAr })
                .ToListAsync();

            return Json(units);
        }

        #endregion
    }
}