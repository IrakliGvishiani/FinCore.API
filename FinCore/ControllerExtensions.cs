using FinCore.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace FinCore
{
    public static class ControllerExtensions
    {
        public static IActionResult ToActionResult(this ControllerBase controller,
            CommonResponse response)
        {
            return controller.StatusCode(response.StatusCode, response);
        }
    }
}
