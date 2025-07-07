using AwesomeAssertions;
using Dosaic.Extensions.NanoIds;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Audit;
using NUnit.Framework;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Tests.Audit
{
    public class HistoryTests
    {
        private static History<TestHistoryModel> GetModel(ChangeState state = ChangeState.Added, Action<History<TestHistoryModel>> configureHistory = null, Action<TestHistoryModel> configureModel = null)
        {
            var sampleModel = new TestHistoryModel { Id = "123", HistoryProperty = "NewName" };
            configureModel?.Invoke(sampleModel);
            var changes = ObjectChanges.Calculate(state, null, sampleModel);
            var history = new History<TestHistoryModel>
            {
                Id = NanoId.NewId<History<TestHistoryModel>>(),
                ForeignId = sampleModel.Id,
                ChangeSet = changes.ToJson(),
                ModifiedBy = "UserId",
                ModifiedUtc = DateTime.UtcNow,
                State = state,
                Model = sampleModel
            };
            configureHistory?.Invoke(history);
            return history;
        }

        [Test]
        public void CanInitializeHistoryEntry()
        {
            var model = GetModel();
            model.Model.Should().NotBeNull();
            model.Model.Id.Should().Be(model.ForeignId);
            model.State.Should().Be(ChangeState.Added);
            model.ModifiedBy.Should().NotBeNull();
            model.ModifiedUtc.Should().BeWithin(TimeSpan.FromSeconds(2));
            ObjectChanges.FromJson(model.ChangeSet).Should().NotBeEmpty();
        }
    }
}
