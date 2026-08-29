namespace EMISAPIS.DTOS
{
    public class RoleDto
    {
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
    }

    public class MenuDto
    {
        public int MenuId { get; set; }
        public string MenuName { get; set; } = string.Empty;
        public string MenuLink { get; set; } = string.Empty;
        public int MenuOrder { get; set; }
        public bool IsActive { get; set; }
    }

    public class SubMenuDto
    {
        public int SubMenuId { get; set; }
        public string SubMenuName { get; set; } = string.Empty;
        public string SubMenuLink { get; set; } = string.Empty;
        public int MenuId { get; set; }
        public string MenuName { get; set; } = string.Empty;
        public int SubMenuOrder { get; set; }
        public bool IsActive { get; set; }
    }

    public class MenuRoleMappingDto
    {
        public int MenuId { get; set; }
        public string MenuName { get; set; } = string.Empty;
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
    }

    public class SubMenuRoleMappingDto
    {
        public int SMControlId { get; set; }
        public int SubMenuId { get; set; }
        public string SubMenuName { get; set; } = string.Empty;
        public string SubMenuLink { get; set; } = string.Empty;
        public int MenuId { get; set; }
        public string MenuName { get; set; } = string.Empty;
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
    }

    public class SubMenuWithRoleStatusDto
    {
        public int SubMenuId { get; set; }
        public string SubMenuName { get; set; } = string.Empty;
        public string SubMenuLink { get; set; } = string.Empty;
        public int MenuId { get; set; }
        public string MenuName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public int AddedOrNot { get; set; }
    }

    public class RoleMenuGridDto
    {
        public int MenuId { get; set; }
        public string MenuName { get; set; } = string.Empty;
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public int NoOfSubMenus { get; set; }
        public int MenuOrder { get; set; }
    }

    public class CreateSubMenuRequest
    {
        public string SubMenuName { get; set; } = string.Empty;
        public string SubMenuLink { get; set; } = string.Empty;
        public int MenuId { get; set; }
    }

    public class MapRoleScreenRequest
    {
        public int RoleId { get; set; }
        public int MenuId { get; set; }
        public List<int> SubMenuIds { get; set; } = new();
    }

    public class RenameMenuRequest
    {
        public string NewName { get; set; } = string.Empty;
    }

    public class RemoveSubMenuRoleRequest
    {
        public int SubMenuId { get; set; }
        public int SMControlId { get; set; }
    }

    public class SidebarItemDto
    {
        public string Label { get; set; } = string.Empty;
        public string Route { get; set; } = string.Empty;
        public List<SidebarSubItemDto> Submenu { get; set; } = new();
    }

    public class SidebarSubItemDto
    {
        public string Label { get; set; } = string.Empty;
        public string Route { get; set; } = string.Empty;
    }
}
