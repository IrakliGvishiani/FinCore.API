using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Http;
namespace FinCore.Application.Exceptions
{
    public class CommonResponse
    {

        public CommonResponse()
        {

        }

        public CommonResponse(
            string message,
            object result,
            bool isSuccess,
            int HttpStatusCode
            )
        {
            Message = message;
            Result = result;
            IsSuccess = isSuccess;
            StatusCode = HttpStatusCode;
        }

        public string Message { get; set; }

        public object Result { get; set; }
        public bool IsSuccess { get; set; }
        public int StatusCode { get; set; }

        public CommonResponse Success (object result)
        {
            return new CommonResponse(
            CommonResponseMessage.SuccessMessage,
            result,
            true,
            StatusCodes.Status200OK);
        }
        public CommonResponse Created(object result)
        {
            return new CommonResponse(
                "Created successfully.",
                result,
                true,
                StatusCodes.Status201Created);
        }

        public CommonResponse BadRequest(string message)
        {
            return new CommonResponse(
                message,
                null,
                false,
                StatusCodes.Status400BadRequest);
        }

        public CommonResponse NotFound(string message)
        {
            return new CommonResponse(
                message,
                null,
                false,
                StatusCodes.Status404NotFound);
        }

        public CommonResponse NoContent()
        {
            return new CommonResponse(
                "No Content",
                null,
                true,
                StatusCodes.Status204NoContent
                );
        }
        public static class CommonResponseMessage
        {
            public static string SuccessMessage { get; } = "Request processed successfully.";
        }
    }
}
