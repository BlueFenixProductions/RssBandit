using System.Collections.Generic;
using System.ComponentModel;
using NUnit.Framework;
using RssBandit.ViewModels;

namespace RssBandit.ViewModels.Tests
{
    [TestFixture]
    public sealed class FeedNodeViewModelTests
    {
        private static FeedItemViewModel Item(bool read) =>
            new FeedItemViewModel(new FakeReadStateItem { BeenRead = read });

        [Test]
        public void UnreadCount_ReflectsUnreadChildren()
        {
            // 3 children, 2 unread => 2.
            var node = new FeedNodeViewModel(new[] { Item(read: false), Item(read: true), Item(read: false) });

            Assert.That(node.UnreadCount, Is.EqualTo(2));
        }

        [Test]
        public void MarkingChildRead_DecrementsUnreadCount()
        {
            var unreadChild = Item(read: false);
            var node = new FeedNodeViewModel(new[] { unreadChild, Item(read: false) });
            Assert.That(node.UnreadCount, Is.EqualTo(2));

            var raised = new List<string?>();
            node.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

            unreadChild.ToggleReadCommand.Execute(null);

            Assert.That(node.UnreadCount, Is.EqualTo(1));
            Assert.That(raised, Does.Contain(nameof(FeedNodeViewModel.UnreadCount)),
                "Marking a child read must raise PropertyChanged(UnreadCount).");
        }

        [Test]
        public void MarkingChildUnread_IncrementsUnreadCount()
        {
            var readChild = Item(read: true);
            var node = new FeedNodeViewModel(new[] { readChild, Item(read: false) });
            Assert.That(node.UnreadCount, Is.EqualTo(1));

            readChild.ToggleReadCommand.Execute(null);

            Assert.That(node.UnreadCount, Is.EqualTo(2));
        }

        [Test]
        public void AddingUnreadItem_IncrementsUnreadCount()
        {
            var node = new FeedNodeViewModel();
            Assert.That(node.UnreadCount, Is.EqualTo(0));

            node.Items.Add(Item(read: false));

            Assert.That(node.UnreadCount, Is.EqualTo(1));
        }
    }
}
