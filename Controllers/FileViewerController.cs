using Microsoft.AspNetCore.Mvc;
using EMISAPIS.Helpers;

namespace EMISAPIS.Controllers
{
        [ApiController]
    [Route("api/[controller]")]
    public class FileViewerController : Controller
    {
        private readonly MongoService _mongoService;

        public FileViewerController()
        {
            _mongoService = new MongoService();
        }

        [HttpGet("view")]
        public async Task<IActionResult> ViewFile(int id, string type)
        {
            var data = await _mongoService.GetFile(id);

            if (data == null)
                return NotFound();

            byte[] fileBytes = null;

            switch (type)
            {
                case "chalan":
                    fileBytes = data.FileChalan;
                    break;

                case "waranty":
                    fileBytes = data.FileWarrantyCard;
                    break;

                case "insPhoto":
                    fileBytes = data.FilePhoto;
                    break;

                case "insReport":
                    fileBytes = data.File;
                    break;
            }

            if (fileBytes == null)
                return NotFound();

            return File(fileBytes, "application/octet-stream");
        }
    }
}