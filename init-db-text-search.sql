DO
$$
BEGIN
    -- 初始化混合中英文搜索配置
    RAISE NOTICE '正在初始化混合中英文搜索配置...';
END
$$;


-- 创建扩展
CREATE EXTENSION IF NOT EXISTS zhparser;

-- 创建混合配置

DO
$$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_ts_config
        WHERE cfgname = 'mixed_zh_en'
    ) THEN
        EXECUTE  $sql$
            CREATE TEXT SEARCH CONFIGURATION mixed_zh_en (PARSER = zhparser);

            -- 配置映射
            -- SELECT * FROM ts_token_type('english_stem');
            ALTER TEXT SEARCH CONFIGURATION mixed_zh_en 
            ADD MAPPING FOR n,v,a,i,e,l WITH simple;

            -- 添加未知词汇映射（x代表未知词汇，通常包含英文）
            ALTER TEXT SEARCH CONFIGURATION mixed_zh_en 
            ADD MAPPING FOR x WITH english_stem;

            -- 添加数字和其他符号
            ALTER TEXT SEARCH CONFIGURATION mixed_zh_en 
            ADD MAPPING FOR w,s WITH simple;

            RAISE NOTICE '混合中英文搜索配置初始化完成！'

        $sql$;
    END IF;
END
$$;



-- 验证配置
SELECT 'Chinese Test:' as test_type, 
       to_tsvector('mixed_zh_en', '人工智能技术发展很快') as result
UNION ALL
SELECT 'English Test:' as test_type, 
       to_tsvector('mixed_zh_en', 'artificial intelligence technology development') as result
UNION ALL
SELECT 'Mixed Test:' as test_type, 
       to_tsvector('mixed_zh_en', '人工智能 artificial intelligence 技术发展 technology development') as result;
