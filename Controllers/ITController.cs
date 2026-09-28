using EMISAPIS.DTOS;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace EMISAPIS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ITController : ControllerBase
    {
        private readonly IConfiguration _config;

        public ITController(IConfiguration config)
        {
            _config = config;
        }

        private string ConnStr() => _config.GetConnectionString("DefaultConnection")!;

        #region Roles

        [HttpGet("roles")]
        public async Task<IActionResult> GetRoles()
        {
            var list = new List<RoleDto>();
            using var conn = new SqlConnection(ConnStr());
            await conn.OpenAsync();
            using var cmd = new SqlCommand("SELECT roleid, rolename FROM masrole ORDER BY roleid", conn);
            using var dr = await cmd.ExecuteReaderAsync();
            while (await dr.ReadAsync())
            {
                list.Add(new RoleDto
                {
                    RoleId = Convert.ToInt32(dr["roleid"]),
                    RoleName = dr["rolename"]?.ToString() ?? string.Empty
                });
            }
            return Ok(list);
        }

        #endregion

        #region Menus

        [HttpGet("menus")]
        public async Task<IActionResult> GetMenus()
        {
            var list = new List<MenuDto>();
            using var conn = new SqlConnection(ConnStr());
            await conn.OpenAsync();
            using var cmd = new SqlCommand(
                "SELECT menuid, menuname, menulink, menuorder, isactive FROM masmenu WHERE menuid NOT IN (103,104) ORDER BY menuorder", conn);
            using var dr = await cmd.ExecuteReaderAsync();
            while (await dr.ReadAsync())
            {
                list.Add(new MenuDto
                {
                    MenuId = Convert.ToInt32(dr["menuid"]),
                    MenuName = dr["menuname"]?.ToString() ?? string.Empty,
                    MenuLink = dr["menulink"]?.ToString() ?? string.Empty,
                    MenuOrder = Convert.ToInt32(dr["menuorder"]),
                    IsActive = Convert.ToBoolean(dr["isactive"])
                });
            }
            return Ok(list);
        }

        [HttpGet("menus/all")]
        public async Task<IActionResult> GetMenusAll()
        {
            var list = new List<MenuDto>();
            using var conn = new SqlConnection(ConnStr());
            await conn.OpenAsync();
            using var cmd = new SqlCommand(
                "SELECT menuid, menuname, menulink, menuorder, isactive FROM masmenu ORDER BY menuorder", conn);
            using var dr = await cmd.ExecuteReaderAsync();
            while (await dr.ReadAsync())
            {
                list.Add(new MenuDto
                {
                    MenuId = Convert.ToInt32(dr["menuid"]),
                    MenuName = dr["menuname"]?.ToString() ?? string.Empty,
                    MenuLink = dr["menulink"]?.ToString() ?? string.Empty,
                    MenuOrder = Convert.ToInt32(dr["menuorder"]),
                    IsActive = Convert.ToBoolean(dr["isactive"])
                });
            }
            return Ok(list);
        }

        [HttpGet("menus/{menuId}/submenus")]
        public async Task<IActionResult> GetSubMenus(int menuId)
        {
            var list = new List<SubMenuDto>();
            using var conn = new SqlConnection(ConnStr());
            await conn.OpenAsync();
            using var cmd = new SqlCommand(@"
                SELECT s.submenuid, s.submenuname, s.submenulink, s.menuid, m.menuname,
                       s.submenuorder, s.isactive
                FROM masSubMenu s
                INNER JOIN masmenu m ON m.menuid = s.menuid
                WHERE s.menuid = @MenuId
                ORDER BY s.submenuorder", conn);
            cmd.Parameters.AddWithValue("@MenuId", menuId);
            using var dr = await cmd.ExecuteReaderAsync();
            while (await dr.ReadAsync())
            {
                list.Add(new SubMenuDto
                {
                    SubMenuId = Convert.ToInt32(dr["submenuid"]),
                    SubMenuName = dr["submenuname"]?.ToString() ?? string.Empty,
                    SubMenuLink = dr["submenulink"]?.ToString() ?? string.Empty,
                    MenuId = Convert.ToInt32(dr["menuid"]),
                    MenuName = dr["menuname"]?.ToString() ?? string.Empty,
                    SubMenuOrder = Convert.ToInt32(dr["submenuorder"]),
                    IsActive = Convert.ToBoolean(dr["isactive"])
                });
            }
            return Ok(list);
        }

        [HttpPost("submenus")]
        public async Task<IActionResult> CreateSubMenu([FromBody] CreateSubMenuRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.SubMenuName) || string.IsNullOrWhiteSpace(req.SubMenuLink))
                return BadRequest(new { message = "SubMenuName and SubMenuLink are required." });

            using var conn = new SqlConnection(ConnStr());
            await conn.OpenAsync();

            using var maxCmd = new SqlCommand(
                "SELECT ISNULL(MAX(ROUND(submenuorder,0)),0)+1 FROM masSubMenu", conn);
            var nextOrder = Convert.ToInt32(await maxCmd.ExecuteScalarAsync());

            var link = req.SubMenuLink.StartsWith("~/") ? req.SubMenuLink : "~/" + req.SubMenuLink;

            using var cmd = new SqlCommand(@"
                INSERT INTO masSubMenu (SubMenuName, SubMenuLink, MenuID, SubMenuOrder, IsActive)
                VALUES (@Name, @Link, @MenuId, @Order, 1)", conn);
            cmd.Parameters.AddWithValue("@Name", req.SubMenuName.Trim());
            cmd.Parameters.AddWithValue("@Link", link);
            cmd.Parameters.AddWithValue("@MenuId", req.MenuId);
            cmd.Parameters.AddWithValue("@Order", nextOrder);
            await cmd.ExecuteNonQueryAsync();

            return Ok(new { message = "Sub-menu created successfully.", submenuOrder = nextOrder });
        }

        [HttpPut("menus/{menuId}")]
        public async Task<IActionResult> RenameMenu(int menuId, [FromBody] RenameMenuRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.NewName))
                return BadRequest(new { message = "NewName is required." });

            using var conn = new SqlConnection(ConnStr());
            await conn.OpenAsync();
            using var cmd = new SqlCommand("UPDATE masMenu SET MenuName = @Name WHERE MenuID = @Id", conn);
            cmd.Parameters.AddWithValue("@Name", req.NewName.Trim());
            cmd.Parameters.AddWithValue("@Id", menuId);
            var rows = await cmd.ExecuteNonQueryAsync();
            if (rows == 0) return NotFound(new { message = "Menu not found." });
            return Ok(new { message = "Menu renamed successfully." });
        }

        [HttpPut("submenus/{id}")]
        public async Task<IActionResult> RenameSubMenu(int id, [FromBody] RenameMenuRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.NewName))
                return BadRequest(new { message = "NewName is required." });

            using var conn = new SqlConnection(ConnStr());
            await conn.OpenAsync();
            using var cmd = new SqlCommand("UPDATE masSubMenu SET SubMenuName = @Name WHERE SubMenuID = @Id", conn);
            cmd.Parameters.AddWithValue("@Name", req.NewName.Trim());
            cmd.Parameters.AddWithValue("@Id", id);
            var rows = await cmd.ExecuteNonQueryAsync();
            if (rows == 0) return NotFound(new { message = "Sub-menu not found." });
            return Ok(new { message = "Sub-menu renamed successfully." });
        }

        #endregion

        #region Role-Screen Mapping

        [HttpGet("menus/{menuId}/submenus-with-role-status")]
        public async Task<IActionResult> GetSubMenusWithRoleStatus(int menuId, [FromQuery] int roleId)
        {
            var list = new List<SubMenuWithRoleStatusDto>();
            using var conn = new SqlConnection(ConnStr());
            await conn.OpenAsync();
            using var cmd = new SqlCommand(@"
                SELECT s.submenuid, s.submenuname, s.submenulink, s.menuid, m.menuname, s.isactive,
                       ISNULL(sr.smcontrolid, 0) AS addedornot
                FROM masSubMenu s
                INNER JOIN masmenu m ON m.menuid = s.menuid
                LEFT OUTER JOIN masSubMenuRole sr ON sr.SubMenuID = s.submenuid AND sr.RoleID = @RoleId
                WHERE s.menuid = @MenuId
                ORDER BY s.submenuid DESC", conn);
            cmd.Parameters.AddWithValue("@MenuId", menuId);
            cmd.Parameters.AddWithValue("@RoleId", roleId);
            using var dr = await cmd.ExecuteReaderAsync();
            while (await dr.ReadAsync())
            {
                list.Add(new SubMenuWithRoleStatusDto
                {
                    SubMenuId = Convert.ToInt32(dr["submenuid"]),
                    SubMenuName = dr["submenuname"]?.ToString() ?? string.Empty,
                    SubMenuLink = dr["submenulink"]?.ToString() ?? string.Empty,
                    MenuId = Convert.ToInt32(dr["menuid"]),
                    MenuName = dr["menuname"]?.ToString() ?? string.Empty,
                    IsActive = Convert.ToBoolean(dr["isactive"]),
                    AddedOrNot = Convert.ToInt32(dr["addedornot"])
                });
            }
            return Ok(list);
        }

        [HttpGet("roles/{roleId}/submenu-mappings")]
        public async Task<IActionResult> GetSubMenuMappingsForRole(int roleId, [FromQuery] int? menuId)
        {
            var list = new List<SubMenuRoleMappingDto>();
            using var conn = new SqlConnection(ConnStr());
            await conn.OpenAsync();

            var sql = @"
                SELECT sr.SMControlid, s.submenuid, s.submenuname, s.submenulink, s.menuid, m.menuname,
                       sr.roleid, r.rolename
                FROM masSubMenu s
                INNER JOIN masmenu m ON m.menuid = s.menuid
                INNER JOIN masSubMenuRole sr ON sr.SubMenuID = s.submenuid
                INNER JOIN masRole r ON r.roleid = sr.roleid
                WHERE sr.roleid = @RoleId";

            if (menuId.HasValue)
                sql += " AND s.menuid = @MenuId";

            sql += " ORDER BY m.menuorder, s.submenuorder";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@RoleId", roleId);
            if (menuId.HasValue)
                cmd.Parameters.AddWithValue("@MenuId", menuId.Value);

            using var dr = await cmd.ExecuteReaderAsync();
            while (await dr.ReadAsync())
            {
                list.Add(new SubMenuRoleMappingDto
                {
                    SMControlId = Convert.ToInt32(dr["SMControlid"]),
                    SubMenuId = Convert.ToInt32(dr["submenuid"]),
                    SubMenuName = dr["submenuname"]?.ToString() ?? string.Empty,
                    SubMenuLink = dr["submenulink"]?.ToString() ?? string.Empty,
                    MenuId = Convert.ToInt32(dr["menuid"]),
                    MenuName = dr["menuname"]?.ToString() ?? string.Empty,
                    RoleId = Convert.ToInt32(dr["roleid"]),
                    RoleName = dr["rolename"]?.ToString() ?? string.Empty
                });
            }
            return Ok(list);
        }

        [HttpPost("roles/map-screens")]
        public async Task<IActionResult> MapScreensToRole([FromBody] MapRoleScreenRequest req)
        {
            if (req.SubMenuIds == null || !req.SubMenuIds.Any())
                return BadRequest(new { message = "At least one SubMenuId is required." });

            using var conn = new SqlConnection(ConnStr());
            await conn.OpenAsync();
            using var tx = (SqlTransaction)await conn.BeginTransactionAsync();

            try
            {
                foreach (var subMenuId in req.SubMenuIds)
                {
                    // Check if already exists
                    using var checkCmd = new SqlCommand(
                        "SELECT COUNT(*) FROM masSubMenuRole WHERE SubMenuID = @SMId AND RoleID = @RoleId", conn, tx);
                    checkCmd.Parameters.AddWithValue("@SMId", subMenuId);
                    checkCmd.Parameters.AddWithValue("@RoleId", req.RoleId);
                    var exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0;

                    if (!exists)
                    {
                        using var insCmd = new SqlCommand(
                            "INSERT INTO masSubMenuRole (SubMenuID, RoleID) VALUES (@SMId, @RoleId)", conn, tx);
                        insCmd.Parameters.AddWithValue("@SMId", subMenuId);
                        insCmd.Parameters.AddWithValue("@RoleId", req.RoleId);
                        await insCmd.ExecuteNonQueryAsync();
                    }
                }

                // Also ensure main menu is mapped
                using var menuCheck = new SqlCommand(
                    "SELECT COUNT(*) FROM masMenuRole WHERE MenuID = @MenuId AND RoleID = @RoleId", conn, tx);
                menuCheck.Parameters.AddWithValue("@MenuId", req.MenuId);
                menuCheck.Parameters.AddWithValue("@RoleId", req.RoleId);
                var menuExists = Convert.ToInt32(await menuCheck.ExecuteScalarAsync()) > 0;

                if (!menuExists)
                {
                    using var menuIns = new SqlCommand(
                        "INSERT INTO masMenuRole (MenuID, RoleID) VALUES (@MenuId, @RoleId)", conn, tx);
                    menuIns.Parameters.AddWithValue("@MenuId", req.MenuId);
                    menuIns.Parameters.AddWithValue("@RoleId", req.RoleId);
                    await menuIns.ExecuteNonQueryAsync();
                }

                await tx.CommitAsync();
                return Ok(new { message = "Screens mapped to role successfully." });
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        #endregion

        #region Delete / Remove Mappings

        [HttpGet("roles/{roleId}/menu-grid")]
        public async Task<IActionResult> GetMenuGridForRole(int roleId)
        {
            var list = new List<RoleMenuGridDto>();
            using var conn = new SqlConnection(ConnStr());
            await conn.OpenAsync();
            using var cmd = new SqlCommand(@"
                SELECT mr.RoleName, m.MenuName, m.MenuID, mr.roleid, m.MenuOrder,
                       ISNULL(nosSubmenu, 0) AS nossubmenu
                FROM masMenuRole r
                INNER JOIN masMenu m ON m.MenuID = r.MenuID
                INNER JOIN masRole mr ON mr.roleid = r.RoleID
                LEFT OUTER JOIN (
                    SELECT COUNT(DISTINCT s.SubMenuID) AS nosSubmenu, sr.RoleID, m.MenuID
                    FROM masSubMenu s
                    INNER JOIN masSubMenuRole sr ON sr.SubMenuID = s.SubMenuID
                    INNER JOIN masMenu m ON m.MenuID = s.MenuID
                    GROUP BY sr.RoleID, m.MenuID
                ) s ON s.MenuID = m.MenuID AND s.RoleID = mr.roleid
                WHERE m.MenuID NOT IN (103,104) AND mr.roleid = @RoleId
                ORDER BY m.MenuOrder", conn);
            cmd.Parameters.AddWithValue("@RoleId", roleId);
            using var dr = await cmd.ExecuteReaderAsync();
            while (await dr.ReadAsync())
            {
                list.Add(new RoleMenuGridDto
                {
                    MenuId = Convert.ToInt32(dr["MenuID"]),
                    MenuName = dr["MenuName"]?.ToString() ?? string.Empty,
                    RoleId = Convert.ToInt32(dr["roleid"]),
                    RoleName = dr["RoleName"]?.ToString() ?? string.Empty,
                    NoOfSubMenus = Convert.ToInt32(dr["nossubmenu"]),
                    MenuOrder = Convert.ToInt32(dr["MenuOrder"])
                });
            }
            return Ok(list);
        }

        [HttpGet("roles/{roleId}/menus/{menuId}/submenus-in-role")]
        public async Task<IActionResult> GetSubMenusInRole(int roleId, int menuId)
        {
            var list = new List<SubMenuRoleMappingDto>();
            using var conn = new SqlConnection(ConnStr());
            await conn.OpenAsync();
            using var cmd = new SqlCommand(@"
                SELECT mr.RoleName, m.MenuName, m.MenuID, mr.roleid,
                       SubMenuName, SubMenuLink, s.SubMenuID, SMControlID
                FROM masMenuRole r
                INNER JOIN masMenu m ON m.MenuID = r.MenuID
                INNER JOIN masRole mr ON mr.roleid = r.RoleID
                LEFT OUTER JOIN (
                    SELECT s.SubMenuID, sr.RoleID, m.MenuID, s.SubMenuName, s.SubMenuLink, sr.SMControlID
                    FROM masSubMenu s
                    INNER JOIN masSubMenuRole sr ON sr.SubMenuID = s.SubMenuID
                    INNER JOIN masMenu m ON m.MenuID = s.MenuID
                ) s ON s.MenuID = m.MenuID AND s.RoleID = mr.roleid
                WHERE mr.roleid = @RoleId AND m.MenuID = @MenuId
                ORDER BY m.MenuOrder", conn);
            cmd.Parameters.AddWithValue("@RoleId", roleId);
            cmd.Parameters.AddWithValue("@MenuId", menuId);
            using var dr = await cmd.ExecuteReaderAsync();
            while (await dr.ReadAsync())
            {
                list.Add(new SubMenuRoleMappingDto
                {
                    SMControlId = dr["SMControlID"] != DBNull.Value ? Convert.ToInt32(dr["SMControlID"]) : 0,
                    SubMenuId = dr["SubMenuID"] != DBNull.Value ? Convert.ToInt32(dr["SubMenuID"]) : 0,
                    SubMenuName = dr["SubMenuName"]?.ToString() ?? string.Empty,
                    SubMenuLink = dr["SubMenuLink"]?.ToString() ?? string.Empty,
                    MenuId = Convert.ToInt32(dr["MenuID"]),
                    MenuName = dr["MenuName"]?.ToString() ?? string.Empty,
                    RoleId = Convert.ToInt32(dr["roleid"]),
                    RoleName = dr["RoleName"]?.ToString() ?? string.Empty
                });
            }
            return Ok(list);
        }

        [HttpDelete("roles/{roleId}/menus/{menuId}")]
        public async Task<IActionResult> DeleteMenuFromRole(int roleId, int menuId)
        {
            using var conn = new SqlConnection(ConnStr());
            await conn.OpenAsync();

            // Check if sub-menus exist
            using var countCmd = new SqlCommand(@"
                SELECT COUNT(*) FROM masSubMenuRole sr
                INNER JOIN masSubMenu s ON s.SubMenuID = sr.SubMenuID
                WHERE sr.RoleID = @RoleId AND s.MenuID = @MenuId", conn);
            countCmd.Parameters.AddWithValue("@RoleId", roleId);
            countCmd.Parameters.AddWithValue("@MenuId", menuId);
            var subCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());

            if (subCount > 0)
                return BadRequest(new { message = "Cannot delete menu — sub-menus still exist for this role." });

            using var cmd = new SqlCommand(
                "DELETE FROM masMenuRole WHERE MenuID = @MenuId AND RoleID = @RoleId", conn);
            cmd.Parameters.AddWithValue("@MenuId", menuId);
            cmd.Parameters.AddWithValue("@RoleId", roleId);
            var rows = await cmd.ExecuteNonQueryAsync();
            if (rows == 0) return NotFound(new { message = "Menu-role mapping not found." });
            return Ok(new { message = "Menu removed from role successfully." });
        }

        [HttpPost("roles/{roleId}/remove-submenus")]
        public async Task<IActionResult> RemoveSubMenusFromRole(int roleId, [FromBody] List<RemoveSubMenuRoleRequest> items)
        {
            if (items == null || !items.Any())
                return BadRequest(new { message = "At least one sub-menu is required." });

            using var conn = new SqlConnection(ConnStr());
            await conn.OpenAsync();
            var removed = 0;
            foreach (var item in items)
            {
                using var cmd = new SqlCommand(
                    "DELETE FROM masSubMenuRole WHERE SubMenuID = @SMId AND SMControlID = @SMCtrlId", conn);
                cmd.Parameters.AddWithValue("@SMId", item.SubMenuId);
                cmd.Parameters.AddWithValue("@SMCtrlId", item.SMControlId);
                removed += await cmd.ExecuteNonQueryAsync();
            }
            return Ok(new { message = $"{removed} sub-menu(s) removed from role.", removed });
        }

        #endregion

        #region Dynamic Sidebar (for future DB-driven menus)

        [HttpGet("sidebar/{roleId}")]
        public async Task<IActionResult> GetSidebarForRole(int roleId)
        {
            var menus = new List<dynamic>();
            using var conn = new SqlConnection(ConnStr());
            await conn.OpenAsync();

            // Get top-level menus for this role (including 103, 104 always)
            using var cmd = new SqlCommand(@"
                SELECT m.MenuOrder, m.menuid, m.menulink, m.MenuName,
                       ISNULL(sm.nos, 0) AS nos
                FROM masMenu m
                INNER JOIN masmenuRole r ON r.menuid = m.menuid
                LEFT OUTER JOIN (
                    SELECT r.RoleID, s.MenuID, COUNT(r.SMcontrolid) AS nos
                    FROM masSubMenu s
                    INNER JOIN masSubMenuRole r ON r.submenuid = s.SubMenuID
                    WHERE r.RoleID = @RoleId
                    GROUP BY s.MenuID, r.RoleID
                ) sm ON sm.RoleID = r.RoleID AND sm.MenuID = m.MenuID
                WHERE m.isActive = 1 AND r.roleid = @RoleId AND ISNULL(sm.nos, 0) > 0
                UNION ALL
                SELECT m.MenuOrder, m.menuid, m.menulink, m.MenuName, 1 AS nos
                FROM masMenu m WHERE MenuID IN (103, 104)
                ORDER BY MenuOrder", conn);
            cmd.Parameters.AddWithValue("@RoleId", roleId);

            using var dr = await cmd.ExecuteReaderAsync();
            while (await dr.ReadAsync())
            {
                var menuId = Convert.ToInt32(dr["menuid"]);
                menus.Add(new
                {
                    MenuId = menuId,
                    MenuName = dr["MenuName"]?.ToString(),
                    MenuLink = dr["menulink"]?.ToString(),
                    MenuOrder = Convert.ToInt32(dr["MenuOrder"]),
                    SubMenuCount = Convert.ToInt32(dr["nos"])
                });
            }

            return Ok(menus);
        }

        [HttpGet("sidebar/{roleId}/menus/{menuId}/submenus")]
        public async Task<IActionResult> GetSideBarSubMenus(int roleId, int menuId)
        {
            var subMenus = new List<SubMenuDto>();
            using var conn = new SqlConnection(ConnStr());
            await conn.OpenAsync();
            using var cmd = new SqlCommand(@"
                SELECT s.submenuid, s.submenuname, s.submenulink, s.menuid, m.menuname,
                       s.submenuorder, s.isactive
                FROM masSubMenu s
                INNER JOIN masSubMenuRole sr ON sr.submenuid = s.submenuid
                WHERE s.isActive = 1 AND s.MenuID = @MenuId AND sr.Roleid = @RoleId
                ORDER BY s.SubMenuOrder", conn);
            cmd.Parameters.AddWithValue("@MenuId", menuId);
            cmd.Parameters.AddWithValue("@RoleId", roleId);
            using var dr = await cmd.ExecuteReaderAsync();
            while (await dr.ReadAsync())
            {
                subMenus.Add(new SubMenuDto
                {
                    SubMenuId = Convert.ToInt32(dr["submenuid"]),
                    SubMenuName = dr["submenuname"]?.ToString() ?? string.Empty,
                    SubMenuLink = dr["submenulink"]?.ToString() ?? string.Empty,
                    MenuId = Convert.ToInt32(dr["menuid"]),
                    MenuName = dr["menuname"]?.ToString() ?? string.Empty,
                    SubMenuOrder = Convert.ToInt32(dr["submenuorder"]),
                    IsActive = Convert.ToBoolean(dr["isactive"])
                });
            }
            return Ok(subMenus);
        }

        [HttpGet("sidebar-tree/{roleId}")]
        public async Task<IActionResult> GetSidebarTreeForRole(int roleId)
        {
            var result = new List<SidebarTreeItemDto>();
            using var conn = new SqlConnection(ConnStr());
            await conn.OpenAsync();

            var sqlMenus = @"
                SELECT DISTINCT m.MenuOrder, m.menuid, m.menulink, m.MenuName
                FROM masMenu m
                INNER JOIN masmenuRole r ON r.menuid = m.menuid
                INNER JOIN masSubMenu s ON s.MenuID = m.MenuID
                INNER JOIN masSubMenuRole sr ON sr.SubMenuID = s.SubMenuID AND sr.RoleID = r.RoleID
                WHERE m.isActive = 1 AND s.isActive = 1 AND r.roleid = @RoleId
                ORDER BY m.MenuOrder";

            var menuMap = new List<(int MenuId, string Label, string Route, int Order)>();

            using (var cmd = new SqlCommand(sqlMenus, conn))
            {
                cmd.Parameters.AddWithValue("@RoleId", roleId);
                using var dr = await cmd.ExecuteReaderAsync();
                while (await dr.ReadAsync())
                {
                    menuMap.Add((
                        Convert.ToInt32(dr["menuid"]),
                        dr["MenuName"]?.ToString() ?? string.Empty,
                        NormalizeRoute(dr["menulink"]?.ToString()),
                        Convert.ToInt32(dr["MenuOrder"])
                    ));
                }
            }

            foreach (var menu in menuMap)
            {
                var item = new SidebarTreeItemDto
                {
                    MenuId = menu.MenuId,
                    Label = menu.Label,
                    Route = menu.Route,
                    Order = menu.Order
                };

                var sqlSub = @"
                    SELECT s.submenuid, s.submenuname, s.submenulink, s.submenuorder
                    FROM masSubMenu s
                    INNER JOIN masSubMenuRole sr ON sr.submenuid = s.submenuid
                    WHERE s.isActive = 1 AND s.MenuID = @MenuId AND sr.Roleid = @RoleId
                    ORDER BY s.SubMenuOrder";

                using (var subCmd = new SqlCommand(sqlSub, conn))
                {
                    subCmd.Parameters.AddWithValue("@MenuId", menu.MenuId);
                    subCmd.Parameters.AddWithValue("@RoleId", roleId);
                    using var subDr = await subCmd.ExecuteReaderAsync();
                    while (await subDr.ReadAsync())
                    {
                        item.Submenu.Add(new SidebarTreeSubItemDto
                        {
                            SubMenuId = Convert.ToInt32(subDr["submenuid"]),
                            Label = subDr["submenuname"]?.ToString() ?? string.Empty,
                            Route = NormalizeRoute(subDr["submenulink"]?.ToString()),
                            Order = Convert.ToInt32(subDr["submenuorder"])
                        });
                    }
                }

                if (item.Submenu.Count > 0 || !string.IsNullOrWhiteSpace(item.Route))
                {
                    result.Add(item);
                }
            }

            return Ok(result);
        }


        private static string NormalizeRoute(string? rawLink)
        {
            if (string.IsNullOrWhiteSpace(rawLink)) return string.Empty;
            var link = rawLink.Trim();
            if (link.StartsWith("~/")) link = link.Substring(2);
            if (!link.StartsWith("/")) link = "/" + link;
            return link;
        }

        #endregion
    }
}

