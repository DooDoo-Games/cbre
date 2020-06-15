﻿using System;

namespace CBRE.Packages
{
    public class PackageException : Exception
    {
        public PackageException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public PackageException()
        {
        }

        public PackageException(string message) : base(message)
        {
        }
    }
}
