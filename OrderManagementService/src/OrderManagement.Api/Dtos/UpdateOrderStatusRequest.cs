namespace OrderManagement.Api.Dtos
{
    // Immutable by design -- represents what the client sent, nothing in
    // the controller should ever need to mutate it after binding.
    public record UpdateOrderStatusRequest(string Status);
}
