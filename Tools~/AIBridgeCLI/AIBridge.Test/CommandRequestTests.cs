namespace AIBridgeCLI.Tests
{
    public class CommandRequestTests
    {
        [Fact]
        public void CommandRequest_WithValues_SetsProperties()
        {
            CommandRequest request = new()
            {
                id = "test_id",
                type = "GameObjectCommand_Find",
                @params = new Dictionary<string, object>
                {
                    { "name", "Player" },
                    { "tag", "Enemy" }
                }
            };

            Assert.Equal("test_id", request.id);
            Assert.Equal("GameObjectCommand_Find", request.type);
            Assert.Equal(2, request.@params.Count);
            Assert.Equal("Player", request.@params["name"]);
            Assert.Equal("Enemy", request.@params["tag"]);
        }
    }
}
