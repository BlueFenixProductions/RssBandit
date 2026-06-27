using System;
using System.Collections.Generic;
using System.ComponentModel;
using NUnit.Framework;
using RssBandit.ViewModels;

namespace RssBandit.ViewModels.Tests
{
    [TestFixture]
    public sealed class FeedItemViewModelTests
    {
        [Test]
        public void ToggleRead_FlipsIsRead()
        {
            var fake = new FakeReadStateItem { BeenRead = false };
            var vm = new FeedItemViewModel(fake);

            Assert.That(vm.IsRead, Is.False);

            vm.ToggleReadCommand.Execute(null);
            Assert.That(vm.IsRead, Is.True);

            vm.ToggleReadCommand.Execute(null);
            Assert.That(vm.IsRead, Is.False);
        }

        [Test]
        public void ToggleRead_RaisesPropertyChanged_ForIsRead()
        {
            var fake = new FakeReadStateItem { BeenRead = false };
            var vm = new FeedItemViewModel(fake);

            var raised = new List<string?>();
            vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

            vm.ToggleReadCommand.Execute(null);

            // Exactly the IsRead property change is raised (no incidental notifications).
            Assert.That(raised, Is.EqualTo(new[] { nameof(FeedItemViewModel.IsRead) }));
        }

        [Test]
        public void ToggleRead_WritesThroughToModel()
        {
            var fake = new FakeReadStateItem { BeenRead = false };
            var vm = new FeedItemViewModel(fake);

            vm.ToggleReadCommand.Execute(null);

            Assert.That(fake.BeenRead, Is.True, "Toggling read must write through to the model.");

            vm.ToggleReadCommand.Execute(null);
            Assert.That(fake.BeenRead, Is.False, "Toggling unread must write through to the model.");
        }

        [Test]
        public void Construction_MirrorsModelReadState()
        {
            var read = new FeedItemViewModel(new FakeReadStateItem { BeenRead = true });
            Assert.That(read.IsRead, Is.True);

            var unread = new FeedItemViewModel(new FakeReadStateItem { BeenRead = false });
            Assert.That(unread.IsRead, Is.False);
        }

        [Test]
        public void Title_Date_Id_ProjectModel()
        {
            var date = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc);
            var fake = new FakeReadStateItem { Id = "id-42", Title = "Hello", Date = date };
            var vm = new FeedItemViewModel(fake);

            Assert.That(vm.Id, Is.EqualTo("id-42"));
            Assert.That(vm.Title, Is.EqualTo("Hello"));
            Assert.That(vm.Date, Is.EqualTo(date));
        }

        [Test]
        public void ToggleReadCommand_CanAlwaysExecute()
        {
            var vm = new FeedItemViewModel(new FakeReadStateItem());
            Assert.That(vm.ToggleReadCommand.CanExecute(null), Is.True);
        }
    }
}
