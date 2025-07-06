
DO
$$
BEGIN
     IF EXISTS (
        SELECT FROM pg_database WHERE datname = 'mini_cc'
    ) THEN
        RAISE NOTICE '数据库 mini_cc 已存在';
     ELSE
        EXECUTE $sql$
            CREATE DATABASE mini_cc
            WITH
                OWNER = postgres
                ENCODING = 'UTF8'
                LC_COLLATE = 'zh_CN.UTF-8'
                LC_CTYPE = 'zh_CN.UTF-8'
                LOCALE_PROVIDER = 'libc'
                TABLESPACE = pg_default
                CONNECTION LIMIT = -1
                IS_TEMPLATE = false;
        $sql$;
    END IF;
END
$$;
