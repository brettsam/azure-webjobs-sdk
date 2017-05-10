// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.WebJobs.Host;

namespace Microsoft.WindowsAzure.Storage.Queue
{
    internal static class CloudQueueMessageExtensions
    {
        /// <summary>
        /// Overwrites the message's properties with the specified values.
        /// </summary>
        public static void SetProperties(this CloudQueueMessage message, string id, string popReceipt, DateTimeOffset? insertionTime,
            DateTimeOffset? nextVisibleTime, DateTimeOffset? expirationTime)
        {
            IReadOnlyList<PropertyHelper> messageProperties = PropertyHelper.GetProperties(typeof(CloudQueueMessage));

            PropertyHelper idProp = messageProperties.Single(p => p.Name == nameof(CloudQueueMessage.Id));
            idProp.SetValue(message, id);

            PropertyHelper popReceiptProp = messageProperties.Single(p => p.Name == nameof(CloudQueueMessage.PopReceipt));
            popReceiptProp.SetValue(message, popReceipt);

            PropertyHelper insertionTimeProp = messageProperties.SingleOrDefault(p => p.Name == nameof(CloudQueueMessage.InsertionTime));
            insertionTimeProp.SetValue(message, insertionTime);

            PropertyHelper nextVisibleTimeProp = messageProperties.SingleOrDefault(p => p.Name == nameof(CloudQueueMessage.NextVisibleTime));
            nextVisibleTimeProp.SetValue(message, nextVisibleTime);

            PropertyHelper expirationTimeProp = messageProperties.SingleOrDefault(p => p.Name == nameof(CloudQueueMessage.ExpirationTime));
            expirationTimeProp.SetValue(message, expirationTime);
        }
    }
}
