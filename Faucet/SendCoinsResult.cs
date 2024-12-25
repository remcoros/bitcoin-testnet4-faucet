// using System.Diagnostics.CodeAnalysis;
//
// namespace Faucet.WebApp.Services;
//
// public class SendCoinsResult
// {
//     [MemberNotNullWhen(true, nameof(TransactionId))]
//     [MemberNotNullWhen(false, nameof(ErrorMessage))]
//     public bool IsSuccess { get; private init; }
//     public string? TransactionId { get; private init;}
//     
//     public string? ErrorMessage { get; private init;}
//
//     private SendCoinsResult()
//     {
//     }
//     
//     public static SendCoinsResult Success(string transactionId)
//     {
//         return new SendCoinsResult
//         {
//             IsSuccess = true,
//             TransactionId = transactionId
//         };
//     }
//     
//     public static SendCoinsResult Failure(string errorMessage)
//     {
//         return new SendCoinsResult
//         {
//             IsSuccess = false,
//             ErrorMessage = errorMessage
//         };
//     }
// }